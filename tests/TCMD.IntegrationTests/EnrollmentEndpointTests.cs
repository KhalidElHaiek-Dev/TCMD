using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TCMD.Domain.Enrollments;
using TCMD.Infrastructure.Persistence;

namespace TCMD.IntegrationTests;

[Collection(DatabaseIntegrationCollection.Name)]
public sealed class EnrollmentEndpointTests(TcmdApiFactory factory)
{
    [Theory]
    [InlineData("Administrator")]
    [InlineData("Staff")]
    public async Task OperationalRoles_CanEnrollListWithdrawAndReactivate(string role)
    {
        using var client = await AuthenticatedClient.CreateAsync(factory, role);
        var student = await CreateStudentAsync(client, "Membership Student");
        var group = await CreateGroupAsync(client, "Membership Group");
        var utcDateBefore = DateOnly.FromDateTime(DateTime.UtcNow);

        var created = await CreateEnrollmentAsync(client, group.Id, student.Id);
        Assert.Equal("Active", created.Status);
        Assert.InRange(created.EnrollmentDate, utcDateBefore, DateOnly.FromDateTime(DateTime.UtcNow));
        Assert.Equal(student.Id, created.StudentId);
        Assert.Equal(group.Id, created.TrainingGroupId);
        Assert.NotEmpty(created.RowVersion);

        var groupRows = await client.GetFromJsonAsync<GroupEnrollmentResponse[]>(
            $"/api/training-groups/{group.Id}/enrollments");
        var groupRow = Assert.Single(groupRows!);
        Assert.Equal(student.StudentNumber, groupRow.Student.StudentNumber);
        Assert.Equal("Membership Student", groupRow.Student.FullName);
        Assert.True(groupRow.Student.IsActive);

        var studentRows = await client.GetFromJsonAsync<StudentEnrollmentResponse[]>(
            $"/api/students/{student.Id}/enrollments");
        var studentRow = Assert.Single(studentRows!);
        Assert.Equal("Membership Group", studentRow.TrainingGroup.Name);
        Assert.Equal("Planned", studentRow.TrainingGroup.Status);
        Assert.Equal(group.CourseId, studentRow.TrainingGroup.CourseId);

        var withdrawn = await CommandAsync(client, created, "withdraw");
        Assert.Equal("Withdrawn", withdrawn.Status);
        var reactivated = await CommandAsync(client, withdrawn, "reactivate");
        Assert.Equal("Active", reactivated.Status);
        Assert.Equal(created.Id, reactivated.Id);
        Assert.Equal(created.EnrollmentDate, reactivated.EnrollmentDate);
        Assert.Equal(created.CreatedAtUtc, reactivated.CreatedAtUtc);
    }

    [Fact]
    public async Task EligibilityRules_RejectInactiveStudentAndTerminalGroups()
    {
        using var client = await AuthenticatedClient.CreateAsync(factory, "Staff");
        var inactiveStudent = await CreateStudentAsync(client, "Inactive Student");
        await DeactivateStudentAsync(client, inactiveStudent);
        var planned = await CreateGroupAsync(client, "Planned Eligible");
        Assert.Equal(HttpStatusCode.Conflict,
            (await client.PostAsJsonAsync($"/api/training-groups/{planned.Id}/enrollments",
                new { studentId = inactiveStudent.Id })).StatusCode);

        var activeStudent = await CreateStudentAsync(client, "Active Student");
        var completed = await CreateGroupAsync(client, "Completed Ineligible");
        completed = await ActivateGroupAsync(client, completed);
        completed = await GroupCommandAsync(client, completed, "complete");
        var cancelled = await CreateGroupAsync(client, "Cancelled Ineligible");
        cancelled = await GroupCommandAsync(client, cancelled, "cancel");

        Assert.Equal(HttpStatusCode.Conflict,
            (await client.PostAsJsonAsync($"/api/training-groups/{completed.Id}/enrollments",
                new { studentId = activeStudent.Id })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await client.PostAsJsonAsync($"/api/training-groups/{cancelled.Id}/enrollments",
                new { studentId = activeStudent.Id })).StatusCode);
    }

    [Fact]
    public async Task PlannedAndActiveGroups_AcceptEnrollmentAndDuplicateIsPermanent()
    {
        using var client = await AuthenticatedClient.CreateAsync(factory, "Staff");
        var student = await CreateStudentAsync(client, "Eligible Student");
        var planned = await CreateGroupAsync(client, "Planned Accepts");
        var active = await ActivateGroupAsync(client, await CreateGroupAsync(client, "Active Accepts"));

        var plannedEnrollment = await CreateEnrollmentAsync(client, planned.Id, student.Id);
        await CreateEnrollmentAsync(client, active.Id, student.Id);
        var withdrawn = await CommandAsync(client, plannedEnrollment, "withdraw");

        using var duplicate = await client.PostAsJsonAsync($"/api/training-groups/{planned.Id}/enrollments",
            new { studentId = student.Id });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("Withdrawn", withdrawn.Status);
        Assert.Single((await client.GetFromJsonAsync<GroupEnrollmentResponse[]>(
            $"/api/training-groups/{planned.Id}/enrollments"))!);
    }

    [Fact]
    public async Task StatusRules_AllowCorrectionsAfterTerminalGroupButRestrictReactivation()
    {
        using var client = await AuthenticatedClient.CreateAsync(factory, "Staff");
        var student = await CreateStudentAsync(client, "Terminal Corrections");

        var completedGroup = await ActivateGroupAsync(client, await CreateGroupAsync(client, "Complete Correction"));
        var enrollmentToComplete = await CreateEnrollmentAsync(client, completedGroup.Id, student.Id);
        completedGroup = await GroupCommandAsync(client, completedGroup, "complete");
        Assert.Equal("Completed", (await CommandAsync(client, enrollmentToComplete, "complete")).Status);

        var cancelledGroup = await CreateGroupAsync(client, "Cancel Correction");
        var enrollmentToWithdraw = await CreateEnrollmentAsync(client, cancelledGroup.Id, student.Id);
        cancelledGroup = await GroupCommandAsync(client, cancelledGroup, "cancel");
        var withdrawn = await CommandAsync(client, enrollmentToWithdraw, "withdraw");
        using var reactivate = await client.PostAsJsonAsync($"/api/enrollments/{withdrawn.Id}/reactivate",
            new { rowVersion = withdrawn.RowVersion });
        Assert.Equal(HttpStatusCode.Conflict, reactivate.StatusCode);
    }

    [Fact]
    public async Task Reactivation_RevalidatesStudentAndCompletedEnrollmentIsTerminal()
    {
        using var client = await AuthenticatedClient.CreateAsync(factory, "Staff");
        var student = await CreateStudentAsync(client, "Revalidation Student");
        var group = await CreateGroupAsync(client, "Revalidation Group");
        var withdrawn = await CommandAsync(client, await CreateEnrollmentAsync(client, group.Id, student.Id), "withdraw");
        await DeactivateStudentAsync(client, student);
        Assert.Equal(HttpStatusCode.Conflict,
            (await client.PostAsJsonAsync($"/api/enrollments/{withdrawn.Id}/reactivate",
                new { rowVersion = withdrawn.RowVersion })).StatusCode);

        var secondStudent = await CreateStudentAsync(client, "Completed Student");
        var completed = await CommandAsync(client, await CreateEnrollmentAsync(client, group.Id, secondStudent.Id), "complete");
        Assert.Equal(HttpStatusCode.Conflict,
            (await client.PostAsJsonAsync($"/api/enrollments/{completed.Id}/reactivate",
                new { rowVersion = completed.RowVersion })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await client.PostAsJsonAsync($"/api/enrollments/{completed.Id}/withdraw",
                new { rowVersion = completed.RowVersion })).StatusCode);
    }

    [Fact]
    public async Task ConcurrencyMalformedVersionsAndIdempotency_AreEnforced()
    {
        using var client = await AuthenticatedClient.CreateAsync(factory, "Staff");
        var student = await CreateStudentAsync(client, "Concurrency Student");
        var group = await CreateGroupAsync(client, "Concurrency Group");
        var created = await CreateEnrollmentAsync(client, group.Id, student.Id);
        var withdrawn = await CommandAsync(client, created, "withdraw");

        using var stale = await client.PostAsJsonAsync($"/api/enrollments/{created.Id}/withdraw",
            new { rowVersion = created.RowVersion });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var malformed = await client.PostAsJsonAsync($"/api/enrollments/{created.Id}/withdraw",
            new { rowVersion = Convert.ToBase64String([1, 2]) });
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
        Assert.NotNull(await malformed.Content.ReadFromJsonAsync<ValidationProblemDetails>());

        var repeated = await CommandAsync(client, withdrawn, "withdraw");
        Assert.Equal(withdrawn.RowVersion, repeated.RowVersion);
        Assert.Equal(withdrawn.LastUpdatedAtUtc, repeated.LastUpdatedAtUtc);
    }

    [Fact]
    public async Task Lists_AreHistoricalDeterministicAndMissingParentsReturnProblemDetails()
    {
        using var client = await AuthenticatedClient.CreateAsync(factory, "Staff");
        var first = await CreateStudentAsync(client, "Zed Student");
        var second = await CreateStudentAsync(client, "Alpha Student");
        var group = await CreateGroupAsync(client, "Historical Group");
        var firstEnrollment = await CreateEnrollmentAsync(client, group.Id, first.Id);
        await CreateEnrollmentAsync(client, group.Id, second.Id);
        await CommandAsync(client, firstEnrollment, "withdraw");

        var rows = (await client.GetFromJsonAsync<GroupEnrollmentResponse[]>(
            $"/api/training-groups/{group.Id}/enrollments"))!;
        Assert.Equal(2, rows.Length);
        Assert.True(string.CompareOrdinal(rows[0].Student.StudentNumber, rows[1].Student.StudentNumber) < 0);
        Assert.Contains(rows, row => row.Status == "Withdrawn");

        using var missingGroup = await client.GetAsync($"/api/training-groups/{Guid.NewGuid()}/enrollments");
        using var missingStudent = await client.GetAsync($"/api/students/{Guid.NewGuid()}/enrollments");
        Assert.Equal(HttpStatusCode.NotFound, missingGroup.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missingStudent.StatusCode);
        Assert.NotNull(await missingGroup.Content.ReadFromJsonAsync<ProblemDetails>());
    }

    [Fact]
    public async Task AnonymousAndInstructor_AreRejectedForEveryEndpointWithoutChangingData()
    {
        using var staff = await AuthenticatedClient.CreateAsync(factory, "Staff");
        var student = await CreateStudentAsync(staff, "Authorization Student");
        var group = await CreateGroupAsync(staff, "Authorization Group");
        var enrollment = await CreateEnrollmentAsync(staff, group.Id, student.Id);
        using var anonymous = factory.CreateClient(new() { AllowAutoRedirect = false });
        using var instructor = await AuthenticatedClient.CreateAsync(factory, "Instructor");
        var requests = new Func<HttpClient, Task<HttpResponseMessage>>[]
        {
            c => c.PostAsJsonAsync($"/api/training-groups/{group.Id}/enrollments", new { studentId = Guid.NewGuid() }),
            c => c.GetAsync($"/api/training-groups/{group.Id}/enrollments"),
            c => c.GetAsync($"/api/students/{student.Id}/enrollments"),
            c => c.PostAsJsonAsync($"/api/enrollments/{enrollment.Id}/complete", new { rowVersion = enrollment.RowVersion }),
            c => c.PostAsJsonAsync($"/api/enrollments/{enrollment.Id}/withdraw", new { rowVersion = enrollment.RowVersion }),
            c => c.PostAsJsonAsync($"/api/enrollments/{enrollment.Id}/reactivate", new { rowVersion = enrollment.RowVersion })
        };
        foreach (var request in requests)
        {
            using var anonymousResponse = await request(anonymous);
            using var instructorResponse = await request(instructor);
            Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, instructorResponse.StatusCode);
        }
        var persisted = Assert.Single((await staff.GetFromJsonAsync<GroupEnrollmentResponse[]>(
            $"/api/training-groups/{group.Id}/enrollments"))!);
        Assert.Equal("Active", persisted.Status);
    }

    [Fact]
    public async Task Database_EnforcesForeignKeysAndPermanentPairUniqueness()
    {
        using var client = await AuthenticatedClient.CreateAsync(factory, "Staff");
        var student = await CreateStudentAsync(client, "Database Student");
        var group = await CreateGroupAsync(client, "Database Group");
        await CreateEnrollmentAsync(client, group.Id, student.Id);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TcmdDbContext>();
        db.Enrollments.Add(Enrollment.Create(student.Id, group.Id, DateOnly.FromDateTime(DateTime.UtcNow), DateTimeOffset.UtcNow));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        db.Enrollments.Add(Enrollment.Create(Guid.NewGuid(), group.Id, DateOnly.FromDateTime(DateTime.UtcNow), DateTimeOffset.UtcNow));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task ConcurrentDuplicateCreation_PersistsExactlyOneEnrollment()
    {
        using var firstClient = await AuthenticatedClient.CreateAsync(factory, "Staff");
        using var secondClient = await AuthenticatedClient.CreateAsync(factory, "Staff");
        var student = await CreateStudentAsync(firstClient, "Concurrent Student");
        var group = await CreateGroupAsync(firstClient, "Concurrent Group");

        var responses = await Task.WhenAll(
            firstClient.PostAsJsonAsync($"/api/training-groups/{group.Id}/enrollments", new { studentId = student.Id }),
            secondClient.PostAsJsonAsync($"/api/training-groups/{group.Id}/enrollments", new { studentId = student.Id }));
        Assert.Contains(responses, response => response.StatusCode == HttpStatusCode.Created);
        Assert.Contains(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        foreach (var response in responses) response.Dispose();
        Assert.Single((await firstClient.GetFromJsonAsync<GroupEnrollmentResponse[]>(
            $"/api/training-groups/{group.Id}/enrollments"))!);
    }

    private static async Task<StudentResponse> CreateStudentAsync(HttpClient client, string name)
    {
        using var response = await client.PostAsJsonAsync("/api/students",
            new { fullName = name, phoneNumber = "555-0100" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<StudentResponse>())!;
    }

    private static async Task DeactivateStudentAsync(HttpClient client, StudentResponse student)
    {
        using var response = await client.PostAsJsonAsync($"/api/students/{student.Id}/deactivate",
            new { rowVersion = student.RowVersion });
        response.EnsureSuccessStatusCode();
    }

    private static async Task<GroupResponse> CreateGroupAsync(HttpClient client, string name)
    {
        using var courseResponse = await client.PostAsJsonAsync("/api/courses",
            new { code = $"C-{Guid.NewGuid():N}", name = "Course" });
        courseResponse.EnsureSuccessStatusCode();
        var course = (await courseResponse.Content.ReadFromJsonAsync<CourseResponse>())!;
        using var instructorResponse = await client.PostAsJsonAsync("/api/instructors",
            new { fullName = $"Instructor {Guid.NewGuid():N}" });
        instructorResponse.EnsureSuccessStatusCode();
        var instructor = (await instructorResponse.Content.ReadFromJsonAsync<InstructorResponse>())!;
        using var response = await client.PostAsJsonAsync("/api/training-groups", new
        {
            name, courseId = course.Id, primaryInstructorId = instructor.Id,
            plannedStartDate = new DateOnly(2026, 9, 1), plannedEndDate = new DateOnly(2026, 12, 1)
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<GroupResponse>())!;
    }

    private static async Task<GroupResponse> ActivateGroupAsync(HttpClient client, GroupResponse group) =>
        await GroupCommandAsync(client, group, "activate");

    private static async Task<GroupResponse> GroupCommandAsync(HttpClient client, GroupResponse group, string command)
    {
        using var response = await client.PostAsJsonAsync($"/api/training-groups/{group.Id}/{command}",
            new { rowVersion = group.RowVersion });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<GroupResponse>())!;
    }

    private static async Task<EnrollmentResponse> CreateEnrollmentAsync(HttpClient client, Guid groupId, Guid studentId)
    {
        using var response = await client.PostAsJsonAsync($"/api/training-groups/{groupId}/enrollments", new { studentId });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var enrollment = (await response.Content.ReadFromJsonAsync<EnrollmentResponse>())!;
        Assert.Equal($"/api/enrollments/{enrollment.Id}", response.Headers.Location?.OriginalString);
        return enrollment;
    }

    private static async Task<EnrollmentResponse> CommandAsync(HttpClient client, EnrollmentResponse enrollment,
        string command)
    {
        using var response = await client.PostAsJsonAsync($"/api/enrollments/{enrollment.Id}/{command}",
            new { rowVersion = enrollment.RowVersion });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<EnrollmentResponse>())!;
    }

    private sealed record StudentResponse(Guid Id, string StudentNumber, string RowVersion);
    private sealed record CourseResponse(Guid Id);
    private sealed record InstructorResponse(Guid Id);
    private sealed record GroupResponse(Guid Id, Guid CourseId, string Status, string RowVersion);
    private sealed record EnrollmentResponse(Guid Id, Guid StudentId, Guid TrainingGroupId, DateOnly EnrollmentDate,
        string Status, DateTimeOffset CreatedAtUtc, DateTimeOffset LastUpdatedAtUtc, string RowVersion);
    private sealed record CompactStudentResponse(Guid Id, string StudentNumber, string FullName, bool IsActive);
    private sealed record GroupEnrollmentResponse(Guid Id, string Status, CompactStudentResponse Student);
    private sealed record CompactGroupResponse(Guid Id, string Name, string Status, Guid CourseId,
        DateOnly PlannedStartDate, DateOnly PlannedEndDate);
    private sealed record StudentEnrollmentResponse(Guid Id, string Status, CompactGroupResponse TrainingGroup);
}
