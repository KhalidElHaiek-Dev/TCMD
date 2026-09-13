using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using TCMD.Infrastructure.Identity;
using TCMD.Infrastructure.Persistence;

namespace TCMD.IntegrationTests;

[Collection(DatabaseIntegrationCollection.Name)]
public sealed class InstructorAssignedAccessEndpointTests(TcmdApiFactory factory) : IAsyncLifetime
{
    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<TcmdDbContext>().AttendanceRecords.ExecuteDeleteAsync();
        factory.ResetUtcNow();
    }

    [Fact]
    public async Task LinkAdministrationIsAdministratorOnly_AndUnlinkedListsAreEmpty()
    {
        using var admin = await AuthenticatedClient.CreateAsync(factory, "Administrator");
        var instructor = await CreateInstructorAsync(admin, "Link Authorization");
        var (name, password) = await AuthenticatedClient.CreateAccountAsync(factory, "Instructor", true);
        var id = await AuthenticatedClient.FindUserIdAsync(factory, name);
        using var staff = await AuthenticatedClient.CreateAsync(factory, "Staff");
        using var instructorClient = await LoginAsync(name, password);
        using var anonymous = await AuthenticatedClient.CreateWithAntiforgeryAsync(factory);
        Assert.Equal(HttpStatusCode.Forbidden, (await PutLinkAsync(staff, id, instructor.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await PutLinkAsync(instructorClient, id, instructor.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await PutLinkAsync(anonymous, id, instructor.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await instructorClient.GetAsync("/api/training-groups")).StatusCode);
        await PutLinkAsync(admin, id, instructor.Id);
        using var linked = await LoginAsync(name, password);
        Assert.Empty((await linked.GetFromJsonAsync<List<GroupResponse>>("/api/training-groups"))!);
    }

    [Fact]
    public async Task DatabaseUniqueIndexRejectsDirectDuplicateInstructorLink()
    {
        using var admin = await AuthenticatedClient.CreateAsync(factory, "Administrator");
        var instructor = await CreateInstructorAsync(admin, "Unique Link");
        var (firstName, _) = await AuthenticatedClient.CreateAccountAsync(factory, "Instructor", true);
        var (secondName, _) = await AuthenticatedClient.CreateAccountAsync(factory, "Instructor", true);
        var firstId = await AuthenticatedClient.FindUserIdAsync(factory, firstName);
        var secondId = await AuthenticatedClient.FindUserIdAsync(factory, secondName);
        Assert.Equal(HttpStatusCode.OK, (await PutLinkAsync(admin, firstId, instructor.Id)).StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TcmdDbContext>();
        var second = await db.Users.SingleAsync(user => user.Id == secondId);
        second.InstructorId = instructor.Id;
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task IdentityConcurrencyStampRejectsStaleAccountLinkUpdate()
    {
        using var staff = await AuthenticatedClient.CreateAsync(factory, "Staff");
        var firstInstructor = await CreateInstructorAsync(staff, "Concurrent First");
        var secondInstructor = await CreateInstructorAsync(staff, "Concurrent Second");
        var (name, _) = await AuthenticatedClient.CreateAccountAsync(factory, "Instructor", true);
        var id = await AuthenticatedClient.FindUserIdAsync(factory, name);
        using var firstScope = factory.Services.CreateScope();
        using var secondScope = factory.Services.CreateScope();
        var firstManager = firstScope.ServiceProvider.GetRequiredService<UserManager<StaffUser>>();
        var secondManager = secondScope.ServiceProvider.GetRequiredService<UserManager<StaffUser>>();
        var firstCopy = (await firstManager.FindByIdAsync(id.ToString()))!;
        var staleCopy = (await secondManager.FindByIdAsync(id.ToString()))!;
        firstCopy.InstructorId = firstInstructor.Id;
        staleCopy.InstructorId = secondInstructor.Id;
        Assert.True((await firstManager.UpdateSecurityStampAsync(firstCopy)).Succeeded);
        var staleResult = await secondManager.UpdateSecurityStampAsync(staleCopy);
        Assert.False(staleResult.Succeeded);
        Assert.Contains(staleResult.Errors, error => error.Code == nameof(IdentityErrorDescriber.ConcurrencyFailure));
    }

    [Fact]
    public async Task Administrator_ManagesIdempotentUniqueActiveInstructorLinks()
    {
        using var admin = await AuthenticatedClient.CreateAsync(factory, "Administrator");
        var firstInstructor = await CreateInstructorAsync(admin, "Linked One");
        var secondInstructor = await CreateInstructorAsync(admin, "Linked Two");
        var (firstName, _) = await AuthenticatedClient.CreateAccountAsync(factory, "Instructor", true);
        var (secondName, _) = await AuthenticatedClient.CreateAccountAsync(factory, "Instructor", true);
        var firstAccount = await AuthenticatedClient.FindUserIdAsync(factory, firstName);
        var secondAccount = await AuthenticatedClient.FindUserIdAsync(factory, secondName);

        var linked = await PutLinkAsync(admin, firstAccount, firstInstructor.Id);
        Assert.Equal(HttpStatusCode.OK, linked.StatusCode);
        Assert.Equal(firstInstructor.Id, (await linked.Content.ReadFromJsonAsync<AccountResponse>())!.InstructorId);
        Assert.Equal(HttpStatusCode.OK, (await PutLinkAsync(admin, firstAccount, firstInstructor.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await PutLinkAsync(admin, secondAccount, firstInstructor.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PutLinkAsync(admin, firstAccount, secondInstructor.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PutLinkAsync(admin, firstAccount, null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PutLinkAsync(admin, firstAccount, null)).StatusCode);

        var (staffName, _) = await AuthenticatedClient.CreateAccountAsync(factory, "Staff", true);
        Assert.Equal(HttpStatusCode.Conflict, (await PutLinkAsync(admin,
            await AuthenticatedClient.FindUserIdAsync(factory, staffName), firstInstructor.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await PutLinkAsync(admin, Guid.NewGuid(), firstInstructor.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await PutLinkAsync(admin, firstAccount, Guid.NewGuid())).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await admin.PutAsJsonAsync("/api/staff-accounts/not-a-guid/instructor-link",
                new { instructorId = firstInstructor.Id })).StatusCode);

        var inactive = await CreateInstructorAsync(admin, "Inactive Link");
        await admin.PostAsJsonAsync($"/api/instructors/{inactive.Id}/deactivate", new { inactive.RowVersion });
        Assert.Equal(HttpStatusCode.Conflict, (await PutLinkAsync(admin, firstAccount, inactive.Id)).StatusCode);
    }

    [Fact]
    public async Task LinkChangesInvalidateSessions_ButTrueNoOpDoesNot()
    {
        using var admin = await AuthenticatedClient.CreateAsync(factory, "Administrator");
        var instructor = await CreateInstructorAsync(admin, "Session Link");
        var (name, password) = await AuthenticatedClient.CreateAccountAsync(factory, "Instructor", true);
        var id = await AuthenticatedClient.FindUserIdAsync(factory, name);
        using var beforeLink = await LoginAsync(name, password);
        Assert.Equal(HttpStatusCode.OK, (await PutLinkAsync(admin, id, instructor.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await beforeLink.GetAsync("/api/training-groups")).StatusCode);

        using var afterLink = await LoginAsync(name, password);
        Assert.Equal(HttpStatusCode.OK, (await PutLinkAsync(admin, id, instructor.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await afterLink.GetAsync("/api/training-groups")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PutLinkAsync(admin, id, null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await afterLink.GetAsync("/api/training-groups")).StatusCode);
        using var unlinked = await LoginAsync(name, password);
        Assert.Equal(HttpStatusCode.Forbidden, (await unlinked.GetAsync("/api/training-groups")).StatusCode);
    }

    [Fact]
    public async Task RoleChangeClearsLink_AndInstructorDeactivationRetainsLinkButRevokesAccess()
    {
        using var admin = await AuthenticatedClient.CreateAsync(factory, "Administrator");
        var instructor = await CreateInstructorAsync(admin, "Role Link");
        var (name, password) = await AuthenticatedClient.CreateAccountAsync(factory, "Instructor", true);
        var id = await AuthenticatedClient.FindUserIdAsync(factory, name);
        await PutLinkAsync(admin, id, instructor.Id);
        using var session = await LoginAsync(name, password);
        var changed = await admin.PatchAsJsonAsync($"/api/staff-accounts/{id}/role", new { role = "Staff" });
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        Assert.Null((await changed.Content.ReadFromJsonAsync<AccountResponse>())!.InstructorId);
        Assert.Equal(HttpStatusCode.Unauthorized, (await session.GetAsync("/api/training-groups")).StatusCode);

        var (secondName, secondPassword) = await AuthenticatedClient.CreateAccountAsync(factory, "Instructor", true);
        var secondId = await AuthenticatedClient.FindUserIdAsync(factory, secondName);
        await PutLinkAsync(admin, secondId, instructor.Id);
        using var linkedSession = await LoginAsync(secondName, secondPassword);
        using var staff = await AuthenticatedClient.CreateAsync(factory, "Staff");
        Assert.Equal(HttpStatusCode.OK, (await staff.PostAsJsonAsync($"/api/instructors/{instructor.Id}/deactivate",
            new { instructor.RowVersion })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await linkedSession.GetAsync("/api/training-groups")).StatusCode);
        using var relogged = await LoginAsync(secondName, secondPassword);
        Assert.Equal(HttpStatusCode.Forbidden, (await relogged.GetAsync("/api/training-groups")).StatusCode);
        var account = await admin.GetFromJsonAsync<AccountResponse>($"/api/staff-accounts/{secondId}");
        Assert.True(account!.IsActive);
        Assert.Equal(instructor.Id, account.InstructorId);
    }

    [Fact]
    public async Task AssignedReadsAreScoped_AndMutationsRemainForbidden()
    {
        using var staff = await AuthenticatedClient.CreateAsync(factory, "Staff");
        using var admin = await AuthenticatedClient.CreateAsync(factory, "Administrator");
        var assigned = await CreateInstructorAsync(staff, "Assigned Access");
        var unrelated = await CreateInstructorAsync(staff, "Unrelated Access");
        var courseId = await CreateCourseAsync(staff);
        var assignedGroup = await CreateGroupAsync(staff, courseId, assigned.Id, "Assigned Group");
        var unrelatedGroup = await CreateGroupAsync(staff, courseId, unrelated.Id, "Unrelated Group");
        var assignedSession = await CreateSessionAsync(staff, assignedGroup.Id);
        var unrelatedSession = await CreateSessionAsync(staff, unrelatedGroup.Id);
        var studentId = await CreateStudentAsync(staff);
        await staff.PostAsJsonAsync($"/api/training-groups/{assignedGroup.Id}/enrollments", new { studentId });

        var (name, password) = await AuthenticatedClient.CreateAccountAsync(factory, "Instructor", true);
        var accountId = await AuthenticatedClient.FindUserIdAsync(factory, name);
        await PutLinkAsync(admin, accountId, assigned.Id);
        using var client = await LoginAsync(name, password);

        var groups = await client.GetFromJsonAsync<List<GroupResponse>>("/api/training-groups");
        Assert.Single(groups!);
        Assert.Equal(assignedGroup.Id, groups![0].Id);
        Assert.Empty((await client.GetFromJsonAsync<List<GroupResponse>>(
            $"/api/training-groups?primaryInstructorId={unrelated.Id}"))!);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/training-groups/{assignedGroup.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/training-groups/{unrelatedGroup.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/training-groups/{assignedGroup.Id}/sessions")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/training-sessions/{unrelatedSession.Id}")).StatusCode);
        var rosterText = await (await client.GetAsync($"/api/training-groups/{assignedGroup.Id}/enrollments")).Content.ReadAsStringAsync();
        Assert.DoesNotContain("phoneNumber", rosterText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("email", rosterText, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync($"/api/training-sessions/{assignedSession.Id}", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/students/{studentId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/students/{studentId}/enrollments")).StatusCode);
    }

    [Fact]
    public async Task InstructorRecordsAndCorrectsAttendance_AndGroupTransferMovesHistoricalAccess()
    {
        factory.SetUtcNow(new DateTimeOffset(2026, 9, 12, 12, 0, 0, TimeSpan.Zero));
        try
        {
            using var staff = await AuthenticatedClient.CreateAsync(factory, "Staff");
            using var admin = await AuthenticatedClient.CreateAsync(factory, "Administrator");
            var first = await CreateInstructorAsync(staff, "Attendance First");
            var second = await CreateInstructorAsync(staff, "Attendance Second");
            var courseId = await CreateCourseAsync(staff);
            var group = await CreateGroupAsync(staff, courseId, first.Id, "Attendance Group");
            var session = await CreateSessionAsync(staff, group.Id);
            var studentId = await CreateStudentAsync(staff);
            var enrollmentResponse = await staff.PostAsJsonAsync($"/api/training-groups/{group.Id}/enrollments",
                new { studentId });
            var enrollmentId = (await enrollmentResponse.Content.ReadFromJsonAsync<IdResponse>())!.Id;

            var firstAccount = await CreateLinkedInstructorClientAsync(admin, first.Id);
            var secondAccount = await CreateLinkedInstructorClientAsync(admin, second.Id);
            using var firstClient = firstAccount.Client;
            using var secondClient = secondAccount.Client;

            var createdResponse = await firstClient.PostAsJsonAsync($"/api/training-sessions/{session.Id}/attendance",
                new { enrollmentId, status = "Present" });
            Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
            var attendance = (await createdResponse.Content.ReadFromJsonAsync<AttendanceResponse>())!;
            Assert.Equal(firstAccount.AccountId, attendance.CreatedByStaffUserId);
            var correctedResponse = await firstClient.PutAsJsonAsync($"/api/attendance/{attendance.Id}", new
                { status = "Late", correctionNote = "Corrected", attendance.RowVersion });
            Assert.Equal(HttpStatusCode.OK, correctedResponse.StatusCode);
            Assert.Equal(firstAccount.AccountId,
                (await correctedResponse.Content.ReadFromJsonAsync<AttendanceResponse>())!.LastUpdatedByStaffUserId);
            Assert.Equal(HttpStatusCode.NotFound,
                (await secondClient.GetAsync($"/api/training-sessions/{session.Id}/attendance")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound,
                (await secondClient.GetAsync($"/api/students/{studentId}/attendance")).StatusCode);

            var transferred = await staff.PutAsJsonAsync($"/api/training-groups/{group.Id}", new
            {
                name = group.Name, courseId, primaryInstructorId = second.Id,
                plannedStartDate = "2026-09-01", plannedEndDate = "2026-09-30", group.RowVersion
            });
            Assert.Equal(HttpStatusCode.OK, transferred.StatusCode);

            foreach (var path in new[]
            {
                $"/api/training-groups/{group.Id}", $"/api/training-sessions/{session.Id}",
                $"/api/training-groups/{group.Id}/enrollments", $"/api/training-groups/{group.Id}/attendance",
                $"/api/students/{studentId}/attendance"
            })
            {
                Assert.Equal(HttpStatusCode.NotFound, (await firstClient.GetAsync(path)).StatusCode);
                Assert.Equal(HttpStatusCode.OK, (await secondClient.GetAsync(path)).StatusCode);
            }
        }
        finally { factory.ResetUtcNow(); }
    }

    private async Task<HttpClient> LoginAsync(string userName, string password)
    {
        return await AuthenticatedClient.LoginAsync(factory, userName, password);
    }

    private static Task<HttpResponseMessage> PutLinkAsync(HttpClient client, Guid accountId, Guid? instructorId) =>
        client.PutAsJsonAsync($"/api/staff-accounts/{accountId}/instructor-link", new { instructorId });

    private static async Task<InstructorResponse> CreateInstructorAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/instructors", new { fullName = $"{name} {Guid.NewGuid():N}" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<InstructorResponse>())!;
    }

    private static async Task<Guid> CreateCourseAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/courses", new
            { code = $"C-{Guid.NewGuid():N}", name = "Assigned access course" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResponse>())!.Id;
    }

    private static async Task<GroupResponse> CreateGroupAsync(HttpClient client, Guid courseId, Guid instructorId,
        string name)
    {
        var response = await client.PostAsJsonAsync("/api/training-groups", new
        {
            name = $"{name} {Guid.NewGuid():N}", courseId, primaryInstructorId = instructorId,
            plannedStartDate = "2026-09-01", plannedEndDate = "2026-09-30"
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<GroupResponse>())!;
    }

    private async Task<(Guid AccountId, HttpClient Client)> CreateLinkedInstructorClientAsync(HttpClient admin,
        Guid instructorId)
    {
        var (name, password) = await AuthenticatedClient.CreateAccountAsync(factory, "Instructor", true);
        var id = await AuthenticatedClient.FindUserIdAsync(factory, name);
        Assert.Equal(HttpStatusCode.OK, (await PutLinkAsync(admin, id, instructorId)).StatusCode);
        return (id, await LoginAsync(name, password));
    }

    private static async Task<IdResponse> CreateSessionAsync(HttpClient client, Guid groupId)
    {
        var response = await client.PostAsJsonAsync($"/api/training-groups/{groupId}/sessions", new
            { sessionDate = "2026-09-12", startTime = "09:00:00", endTime = "10:00:00" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResponse>())!;
    }

    private static async Task<Guid> CreateStudentAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/students", new
            { fullName = "Assigned Student", phoneNumber = "0600000000" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResponse>())!.Id;
    }

    private sealed record AccountResponse(Guid Id, string UserName, string DisplayName, string Role, bool IsActive,
        Guid? InstructorId);
    private sealed record IdResponse(Guid Id);
    private sealed record InstructorResponse(Guid Id, bool IsActive, byte[] RowVersion);
    private sealed record GroupResponse(Guid Id, string Name, byte[] RowVersion);
    private sealed record AttendanceResponse(Guid Id, Guid CreatedByStaffUserId, Guid LastUpdatedByStaffUserId,
        byte[] RowVersion);
}
