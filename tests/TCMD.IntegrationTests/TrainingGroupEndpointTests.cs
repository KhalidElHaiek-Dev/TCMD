using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TCMD.Domain.TrainingGroups;
using TCMD.Infrastructure.Persistence;

namespace TCMD.IntegrationTests;

[Collection(DatabaseIntegrationCollection.Name)]
public sealed class TrainingGroupEndpointTests(TcmdApiFactory factory)
{
    private static readonly DateOnly Start = new(2026, 10, 1);
    private static readonly DateOnly End = new(2026, 12, 1);

    [Theory]
    [InlineData("Administrator")]
    [InlineData("Staff")]
    public async Task OperationalRoles_CanRunCompleteWorkflow(string role)
    {
        using var client = await AuthenticatedClient.CreateAsync(factory, role);
        var course = await CreateCourseAsync(client);
        var instructor = await CreateInstructorAsync(client);
        var created = await CreateGroupAsync(client, course.Id, instructor.Id, "  Evening Group  ");
        Assert.Equal("Evening Group", created.Name);
        Assert.Equal("Planned", created.Status);
        Assert.Equal(course.Id, created.CourseId);
        Assert.Equal(instructor.Id, created.PrimaryInstructorId);
        Assert.Equal(created, await client.GetFromJsonAsync<GroupResponse>($"/api/training-groups/{created.Id}"));

        var updated = await UpdateGroupAsync(client, created, "Updated", course.Id, instructor.Id, Start, End.AddDays(1));
        Assert.NotEqual(created.RowVersion, updated.RowVersion);
        var active = await ChangeStatusAsync(client, updated, "activate");
        Assert.Equal("Active", active.Status);
        var completed = await ChangeStatusAsync(client, active, "complete");
        Assert.Equal("Completed", completed.Status);
        Assert.Equal(completed, await client.GetFromJsonAsync<GroupResponse>($"/api/training-groups/{created.Id}"));
    }

    [Fact]
    public async Task SearchAndFilters_ReturnHistoricalGroupsInDeterministicOrder()
    {
        using var client = await AuthenticatedClient.CreateAsync(factory, "Staff");
        var course = await CreateCourseAsync(client);
        var otherCourse = await CreateCourseAsync(client);
        var instructor = await CreateInstructorAsync(client);
        var marker = Guid.NewGuid().ToString("N");
        var later = await CreateGroupAsync(client, course.Id, instructor.Id, $"Z {marker}", Start.AddDays(1), End);
        var earlier = await CreateGroupAsync(client, course.Id, null, $"A {marker}", Start, End);
        var other = await CreateGroupAsync(client, otherCourse.Id, instructor.Id, $"Other {marker}", Start, End);
        var cancelled = await ChangeStatusAsync(client, later, "cancel");

        var search = (await client.GetFromJsonAsync<List<GroupResponse>>($"/api/training-groups?search={marker}"))!;
        Assert.True(search.FindIndex(x => x.Id == earlier.Id) < search.FindIndex(x => x.Id == cancelled.Id));
        Assert.Contains(search, x => x.Id == cancelled.Id);
        Assert.Contains((await client.GetFromJsonAsync<List<GroupResponse>>($"/api/training-groups?status=Cancelled"))!, x => x.Id == cancelled.Id);
        var byCourse = (await client.GetFromJsonAsync<List<GroupResponse>>($"/api/training-groups?courseId={course.Id}"))!;
        Assert.Contains(byCourse, x => x.Id == earlier.Id);
        Assert.DoesNotContain(byCourse, x => x.Id == other.Id);
        Assert.Contains((await client.GetFromJsonAsync<List<GroupResponse>>($"/api/training-groups?primaryInstructorId={instructor.Id}"))!, x => x.Id == other.Id);
        Assert.Contains((await client.GetFromJsonAsync<List<GroupResponse>>("/api/training-groups?hasPrimaryInstructor=false"))!, x => x.Id == earlier.Id);
        Assert.Empty((await client.GetFromJsonAsync<List<GroupResponse>>($"/api/training-groups?search={Guid.NewGuid():N}"))!);
    }

    [Fact]
    public async Task InactiveReferences_CannotBeSelectedButExistingAssignmentsRemain()
    {
        using var client = await AuthenticatedClient.CreateAsync(factory, "Staff");
        var course = await CreateCourseAsync(client);
        var instructor = await CreateInstructorAsync(client);
        var group = await CreateGroupAsync(client, course.Id, instructor.Id);
        course = await DeactivateCourseAsync(client, course);
        instructor = await DeactivateInstructorAsync(client, instructor);

        using var inactiveCourseCreate = await client.PostAsJsonAsync("/api/training-groups", Request("Blocked", course.Id, null));
        Assert.Equal(HttpStatusCode.Conflict, inactiveCourseCreate.StatusCode);
        var otherCourse = await CreateCourseAsync(client);
        using var inactiveInstructorCreate = await client.PostAsJsonAsync("/api/training-groups", Request("Blocked", otherCourse.Id, instructor.Id));
        Assert.Equal(HttpStatusCode.Conflict, inactiveInstructorCreate.StatusCode);

        var corrected = await UpdateGroupAsync(client, group, "Historical correction", group.CourseId,
            group.PrimaryInstructorId, group.PlannedStartDate, group.PlannedEndDate);
        Assert.Equal(course.Id, corrected.CourseId);
        Assert.Equal(instructor.Id, corrected.PrimaryInstructorId);
        using var activate = await client.PostAsJsonAsync($"/api/training-groups/{corrected.Id}/activate", new { rowVersion = corrected.RowVersion });
        Assert.Equal(HttpStatusCode.Conflict, activate.StatusCode);
    }

    [Fact]
    public async Task UpdateRules_AreEnforcedForPlannedActiveAndTerminalGroups()
    {
        using var client = await AuthenticatedClient.CreateAsync(factory, "Staff");
        var firstCourse = await CreateCourseAsync(client);
        var secondCourse = await CreateCourseAsync(client);
        var firstInstructor = await CreateInstructorAsync(client);
        var secondInstructor = await CreateInstructorAsync(client);
        var group = await CreateGroupAsync(client, firstCourse.Id, null);
        group = await UpdateGroupAsync(client, group, "Planned edit", secondCourse.Id, firstInstructor.Id, Start, End);
        group = await ChangeStatusAsync(client, group, "activate");
        group = await UpdateGroupAsync(client, group, "Active edit", secondCourse.Id, secondInstructor.Id, Start, End.AddDays(2));

        foreach (var request in new[]
        {
            Request("No course change", firstCourse.Id, secondInstructor.Id, group.RowVersion, Start, End),
            Request("No instructor removal", secondCourse.Id, null, group.RowVersion, Start, End)
        })
        {
            using var rejected = await client.PutAsJsonAsync($"/api/training-groups/{group.Id}", request);
            Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
        }
        Assert.Equal(group, await client.GetFromJsonAsync<GroupResponse>($"/api/training-groups/{group.Id}"));

        var cancelled = await ChangeStatusAsync(client, group, "cancel");
        using var terminalEdit = await client.PutAsJsonAsync($"/api/training-groups/{group.Id}",
            Request("Terminal edit", secondCourse.Id, secondInstructor.Id, cancelled.RowVersion, Start, End));
        Assert.Equal(HttpStatusCode.Conflict, terminalEdit.StatusCode);
    }

    [Fact]
    public async Task DuplicateBusinessKey_IsDatabaseEnforcedIncludingConcurrentCreates()
    {
        using var firstClient = await AuthenticatedClient.CreateAsync(factory, "Staff");
        using var secondClient = await AuthenticatedClient.CreateAsync(factory, "Staff");
        var course = await CreateCourseAsync(firstClient);
        var name = $"Duplicate {Guid.NewGuid():N}";
        await CreateGroupAsync(firstClient, course.Id, null, name);
        using var duplicate = await firstClient.PostAsJsonAsync("/api/training-groups", Request($"  {name.ToUpperInvariant()}  ", course.Id, null));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        var raceName = $"Race {Guid.NewGuid():N}";
        var responses = await Task.WhenAll(
            firstClient.PostAsJsonAsync("/api/training-groups", Request(raceName, course.Id, null)),
            secondClient.PostAsJsonAsync("/api/training-groups", Request(raceName.ToUpperInvariant(), course.Id, null)));
        using var first = responses[0];
        using var second = responses[1];
        Assert.Contains(responses, response => response.StatusCode == HttpStatusCode.Created);
        Assert.Contains(responses, response => response.StatusCode == HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ValidationMissingReferencesAndActivationRules_ReturnProblemDetails()
    {
        using var client = await AuthenticatedClient.CreateAsync(factory, "Staff");
        using var invalid = await client.PostAsJsonAsync("/api/training-groups", new
        {
            name = new string('x', 201), courseId = Guid.Empty, primaryInstructorId = Guid.Empty,
            plannedStartDate = End, plannedEndDate = Start
        });
        var validation = await invalid.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal("application/problem+json", invalid.Content.Headers.ContentType?.MediaType);
        Assert.Contains("name", validation!.Errors);
        Assert.Contains("courseId", validation.Errors);
        Assert.Contains("primaryInstructorId", validation.Errors);
        Assert.Contains("plannedEndDate", validation.Errors);
        Assert.True(validation.Extensions.ContainsKey("traceId"));

        using var invalidFilter = await client.GetAsync("/api/training-groups?status=Unknown");
        Assert.Equal(HttpStatusCode.BadRequest, invalidFilter.StatusCode);

        using var missingCourse = await client.PostAsJsonAsync("/api/training-groups", Request("Missing", Guid.NewGuid(), null));
        Assert.Equal(HttpStatusCode.NotFound, missingCourse.StatusCode);
        var course = await CreateCourseAsync(client);
        using var missingInstructor = await client.PostAsJsonAsync("/api/training-groups", Request("Missing", course.Id, Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.NotFound, missingInstructor.StatusCode);
        var unassigned = await CreateGroupAsync(client, course.Id, null);
        using var activate = await client.PostAsJsonAsync($"/api/training-groups/{unassigned.Id}/activate", new { rowVersion = unassigned.RowVersion });
        Assert.Equal(HttpStatusCode.Conflict, activate.StatusCode);
    }

    [Fact]
    public async Task StaleWritesAndMalformedVersions_AreRejectedAndPreserveNewerData()
    {
        using var client = await AuthenticatedClient.CreateAsync(factory, "Staff");
        var course = await CreateCourseAsync(client);
        var instructor = await CreateInstructorAsync(client);
        var original = await CreateGroupAsync(client, course.Id, instructor.Id);
        var newer = await UpdateGroupAsync(client, original, "Newer", course.Id, instructor.Id, Start, End);

        using var staleUpdate = await client.PutAsJsonAsync($"/api/training-groups/{original.Id}",
            Request("Stale", course.Id, instructor.Id, original.RowVersion, Start, End));
        using var staleCancel = await client.PostAsJsonAsync($"/api/training-groups/{original.Id}/cancel", new { rowVersion = original.RowVersion });
        Assert.Equal(HttpStatusCode.Conflict, staleUpdate.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, staleCancel.StatusCode);
        Assert.Equal(newer, await client.GetFromJsonAsync<GroupResponse>($"/api/training-groups/{original.Id}"));
        using var malformed = await client.PostAsJsonAsync($"/api/training-groups/{original.Id}/cancel",
            new { rowVersion = Convert.ToBase64String(new byte[7]) });
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
    }

    [Fact]
    public async Task CompletionAndCancellation_AreIdempotentButStillRejectStaleVersions()
    {
        using var client = await AuthenticatedClient.CreateAsync(factory, "Staff");
        var course = await CreateCourseAsync(client);
        var instructor = await CreateInstructorAsync(client);
        var active = await ChangeStatusAsync(client, await CreateGroupAsync(client, course.Id, instructor.Id), "activate");
        var completed = await ChangeStatusAsync(client, active, "complete");
        var repeatedCompletion = await ChangeStatusAsync(client, completed, "complete");
        Assert.Equal(completed.RowVersion, repeatedCompletion.RowVersion);
        Assert.Equal(completed.LastUpdatedAtUtc, repeatedCompletion.LastUpdatedAtUtc);
        using var stale = await client.PostAsJsonAsync($"/api/training-groups/{completed.Id}/complete", new { rowVersion = active.RowVersion });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

        var planned = await CreateGroupAsync(client, course.Id, null);
        var cancelled = await ChangeStatusAsync(client, planned, "cancel");
        var repeatedCancellation = await ChangeStatusAsync(client, cancelled, "cancel");
        Assert.Equal(cancelled.RowVersion, repeatedCancellation.RowVersion);
        Assert.Equal(cancelled.LastUpdatedAtUtc, repeatedCancellation.LastUpdatedAtUtc);
    }

    [Fact]
    public async Task AnonymousAndInstructor_AreRejectedForEveryEndpoint()
    {
        using var staff = await AuthenticatedClient.CreateAsync(factory, "Staff");
        var course = await CreateCourseAsync(staff);
        var group = await CreateGroupAsync(staff, course.Id, null);
        using var anonymous = await AuthenticatedClient.CreateWithAntiforgeryAsync(factory);
        using var instructor = await AuthenticatedClient.CreateAsync(factory, "Instructor");
        foreach (var (client, expected) in new[] { (anonymous, HttpStatusCode.Unauthorized), (instructor, HttpStatusCode.Forbidden) })
        {
            Assert.Equal(expected, (await client.PostAsJsonAsync("/api/training-groups", Request("Blocked", course.Id, null))).StatusCode);
            Assert.Equal(expected, (await client.GetAsync("/api/training-groups")).StatusCode);
            Assert.Equal(expected, (await client.GetAsync($"/api/training-groups/{group.Id}")).StatusCode);
            Assert.Equal(expected, (await client.PutAsJsonAsync($"/api/training-groups/{group.Id}", Request("Blocked", course.Id, null, group.RowVersion))).StatusCode);
            foreach (var command in new[] { "activate", "complete", "cancel" })
                Assert.Equal(expected, (await client.PostAsJsonAsync($"/api/training-groups/{group.Id}/{command}", new { rowVersion = group.RowVersion })).StatusCode);
        }
    }

    [Fact]
    public async Task DatabaseForeignKeys_RejectMissingReferences()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TcmdDbContext>();
        db.TrainingGroups.Add(TrainingGroup.Create($"FK {Guid.NewGuid():N}", Guid.NewGuid(), null, Start, End, DateTimeOffset.UtcNow));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    private static object Request(string name, Guid courseId, Guid? instructorId, string? rowVersion = null,
        DateOnly? start = null, DateOnly? end = null) => new
        {
            name, courseId, primaryInstructorId = instructorId, plannedStartDate = start ?? Start,
            plannedEndDate = end ?? End, rowVersion
        };

    private static async Task<GroupResponse> CreateGroupAsync(HttpClient client, Guid courseId, Guid? instructorId,
        string? name = null, DateOnly? start = null, DateOnly? end = null)
    {
        using var response = await client.PostAsJsonAsync("/api/training-groups",
            Request(name ?? $"Group {Guid.NewGuid():N}", courseId, instructorId, null, start, end));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var group = (await response.Content.ReadFromJsonAsync<GroupResponse>())!;
        Assert.Equal($"/api/training-groups/{group.Id}", response.Headers.Location?.OriginalString);
        Assert.NotEmpty(group.RowVersion);
        return group;
    }

    private static async Task<GroupResponse> UpdateGroupAsync(HttpClient client, GroupResponse group, string name,
        Guid courseId, Guid? instructorId, DateOnly start, DateOnly end)
    {
        using var response = await client.PutAsJsonAsync($"/api/training-groups/{group.Id}",
            Request(name, courseId, instructorId, group.RowVersion, start, end));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<GroupResponse>())!;
    }

    private static async Task<GroupResponse> ChangeStatusAsync(HttpClient client, GroupResponse group, string command)
    {
        using var response = await client.PostAsJsonAsync($"/api/training-groups/{group.Id}/{command}", new { rowVersion = group.RowVersion });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<GroupResponse>())!;
    }

    private static async Task<CourseResponse> CreateCourseAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/api/courses", new { code = $"C-{Guid.NewGuid():N}", name = "Course" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CourseResponse>())!;
    }

    private static async Task<CourseResponse> DeactivateCourseAsync(HttpClient client, CourseResponse course)
    {
        using var response = await client.PostAsJsonAsync($"/api/courses/{course.Id}/deactivate", new { rowVersion = course.RowVersion });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CourseResponse>())!;
    }

    private static async Task<InstructorResponse> CreateInstructorAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/api/instructors", new { fullName = $"Instructor {Guid.NewGuid():N}" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<InstructorResponse>())!;
    }

    private static async Task<InstructorResponse> DeactivateInstructorAsync(HttpClient client, InstructorResponse instructor)
    {
        using var response = await client.PostAsJsonAsync($"/api/instructors/{instructor.Id}/deactivate", new { rowVersion = instructor.RowVersion });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<InstructorResponse>())!;
    }

    private sealed record GroupResponse(Guid Id, string Name, Guid CourseId, Guid? PrimaryInstructorId,
        DateOnly PlannedStartDate, DateOnly PlannedEndDate, string Status, DateTimeOffset CreatedAtUtc,
        DateTimeOffset LastUpdatedAtUtc, string RowVersion);
    private sealed record CourseResponse(Guid Id, string RowVersion);
    private sealed record InstructorResponse(Guid Id, string RowVersion);
}
