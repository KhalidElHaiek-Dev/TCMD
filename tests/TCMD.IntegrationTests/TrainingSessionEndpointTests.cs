using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TCMD.Infrastructure.Persistence;

namespace TCMD.IntegrationTests;

[Collection(DatabaseIntegrationCollection.Name)]
public sealed class TrainingSessionEndpointTests(TcmdApiFactory factory)
{
    [Theory]
    [InlineData("Administrator")]
    [InlineData("Staff")]
    public async Task OperationalStaff_CreateRetrieveAndListSession(string role)
    {
        using var client = await AuthenticatedClient.CreateAsync(factory, role);
        var group = await CreateGroupAsync(client, $"Create {role}");
        var created = await CreateSessionAsync(client, group.Id, new(2026, 9, 15), new(9, 0), new(11, 0), "  Room A  ");
        Assert.Equal("Room A", created.Location);
        Assert.Equal("Scheduled", created.Status);
        Assert.Equal(8, Convert.FromBase64String(created.RowVersion).Length);
        var fetched = await client.GetFromJsonAsync<SessionResponse>($"/api/training-sessions/{created.Id}");
        Assert.Equal(created.Id, fetched!.Id);
        Assert.Contains((await client.GetFromJsonAsync<SessionResponse[]>($"/api/training-groups/{group.Id}/sessions"))!, x => x.Id == created.Id);
    }

    [Fact]
    public async Task Creation_EnforcesParentStatusAndInclusiveDateRange()
    {
        using var client = await AuthenticatedClient.CreateAsync(factory, "Staff");
        var planned = await CreateGroupAsync(client, "Boundaries");
        await CreateSessionAsync(client, planned.Id, new(2026, 9, 1), new(9, 0), new(10, 0));
        await CreateSessionAsync(client, planned.Id, new(2026, 12, 1), new(9, 0), new(10, 0));
        Assert.Equal(HttpStatusCode.Conflict, (await PostSessionAsync(client, planned.Id, new(2026, 8, 31))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await PostSessionAsync(client, planned.Id, new(2026, 12, 2))).StatusCode);
        Assert.Equal(2, (await client.GetFromJsonAsync<SessionResponse[]>($"/api/training-groups/{planned.Id}/sessions"))!.Length);
        var active = await GroupCommandAsync(client, planned, "activate");
        await CreateSessionAsync(client, active.Id, new(2026, 10, 1), new(9, 0), new(10, 0));
        var completed = await GroupCommandAsync(client, active, "complete");
        Assert.Equal(HttpStatusCode.Conflict, (await PostSessionAsync(client, completed.Id, new(2026, 10, 2))).StatusCode);
        var cancelled = await GroupCommandAsync(client, await CreateGroupAsync(client, "Cancelled Parent"), "cancel");
        Assert.Equal(HttpStatusCode.Conflict, (await PostSessionAsync(client, cancelled.Id, new(2026, 10, 2))).StatusCode);
    }

    [Fact]
    public async Task Validation_NormalizesLocationAndRejectsInvalidValues()
    {
        using var client = await AuthenticatedClient.CreateAsync(factory, "Staff");
        var group = await CreateGroupAsync(client, "Validation");
        Assert.Null((await CreateSessionAsync(client, group.Id, new(2026, 9, 2), new(9, 0), new(10, 0), "   ")).Location);
        Assert.Equal(500, (await CreateSessionAsync(client, group.Id, new(2026, 9, 3), new(9, 0), new(10, 0), new string('x', 500))).Location!.Length);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostSessionAsync(client, group.Id, new(2026, 9, 4), new(9, 0), new(9, 0))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostSessionAsync(client, group.Id, new(2026, 9, 4), new(10, 0), new(9, 0))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostSessionAsync(client, group.Id, new(2026, 9, 4), location: new string('x', 501))).StatusCode);
    }

    [Fact]
    public async Task List_IsHistoricalChronologicalAndMissingResourcesReturnProblemDetails()
    {
        using var client = await AuthenticatedClient.CreateAsync(factory, "Staff");
        var group = await CreateGroupAsync(client, "Ordering");
        var late = await CreateSessionAsync(client, group.Id, new(2026, 10, 2), new(14, 0), new(16, 0));
        var early = await CreateSessionAsync(client, group.Id, new(2026, 10, 1), new(9, 0), new(11, 0));
        var middle = await CreateSessionAsync(client, group.Id, new(2026, 10, 2), new(9, 0), new(10, 0));
        await SessionCommandAsync(client, early, "complete");
        await SessionCommandAsync(client, late, "cancel");
        var rows = (await client.GetFromJsonAsync<SessionResponse[]>($"/api/training-groups/{group.Id}/sessions"))!;
        Assert.Equal([early.Id, middle.Id, late.Id], rows.Select(x => x.Id));
        Assert.Contains(rows, x => x.Status == "Completed");
        Assert.Contains(rows, x => x.Status == "Cancelled");
        Assert.Empty((await client.GetFromJsonAsync<SessionResponse[]>($"/api/training-groups/{(await CreateGroupAsync(client, "Empty")).Id}/sessions"))!);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/training-groups/{Guid.NewGuid()}/sessions")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/training-sessions/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task Update_RespectsSessionAndParentLifecycle()
    {
        using var client = await AuthenticatedClient.CreateAsync(factory, "Staff");
        var group = await CreateGroupAsync(client, "Update Planned");
        var session = await CreateSessionAsync(client, group.Id, new(2026, 9, 15), new(9, 0), new(10, 0));
        Assert.Equal(HttpStatusCode.Conflict, (await PutSessionAsync(client, session, new(2026, 8, 31))).StatusCode);
        Assert.Equal(new DateOnly(2026, 9, 15),
            (await client.GetFromJsonAsync<SessionResponse>($"/api/training-sessions/{session.Id}"))!.SessionDate);
        session = await UpdateSessionAsync(client, session, new(2026, 9, 2), "Room B");
        Assert.Equal("Room B", session.Location);
        Assert.Equal(new DateOnly(2026, 9, 2), session.SessionDate); // Past relative to the approved implementation date is editable.
        group = await GroupCommandAsync(client, group, "activate");
        session = await UpdateSessionAsync(client, session, new(2026, 9, 17), "Room C");
        group = await GroupCommandAsync(client, group, "complete");
        Assert.Equal(HttpStatusCode.Conflict, (await PutSessionAsync(client, session, new(2026, 9, 18))).StatusCode);

        var terminalGroup = await CreateGroupAsync(client, "Terminal Session");
        var completedSession = await SessionCommandAsync(client,
            await CreateSessionAsync(client, terminalGroup.Id, new(2026, 9, 15), new(9, 0), new(10, 0)), "complete");
        Assert.Equal(HttpStatusCode.Conflict, (await PutSessionAsync(client, completedSession, new(2026, 9, 16))).StatusCode);
    }

    [Fact]
    public async Task Commands_WorkAfterTerminalParentAndEnforceTerminalTransitions()
    {
        using var client = await AuthenticatedClient.CreateAsync(factory, "Staff");
        var completedParent = await CreateGroupAsync(client, "Complete After Parent");
        var first = await CreateSessionAsync(client, completedParent.Id, new(2026, 9, 15), new(9, 0), new(10, 0));
        completedParent = await GroupCommandAsync(client, completedParent, "activate");
        await GroupCommandAsync(client, completedParent, "complete");
        var completed = await SessionCommandAsync(client, first, "complete");
        var repeated = await SessionCommandAsync(client, completed, "complete");
        Assert.Equal(completed.RowVersion, repeated.RowVersion);
        Assert.Equal(completed.LastUpdatedAtUtc, repeated.LastUpdatedAtUtc);
        Assert.Equal(HttpStatusCode.Conflict, (await CommandResponseAsync(client, completed, "cancel")).StatusCode);

        var cancelledParent = await CreateGroupAsync(client, "Cancel After Parent");
        var second = await CreateSessionAsync(client, cancelledParent.Id, new(2026, 9, 15), new(9, 0), new(10, 0));
        await GroupCommandAsync(client, cancelledParent, "cancel");
        var cancelled = await SessionCommandAsync(client, second, "cancel");
        var repeatedCancel = await SessionCommandAsync(client, cancelled, "cancel");
        Assert.Equal(cancelled.RowVersion, repeatedCancel.RowVersion);
        Assert.Equal(cancelled.LastUpdatedAtUtc, repeatedCancel.LastUpdatedAtUtc);
        Assert.Equal(HttpStatusCode.Conflict, (await CommandResponseAsync(client, cancelled, "complete")).StatusCode);
    }

    [Fact]
    public async Task Concurrency_RejectsStaleWritesAndMalformedVersions()
    {
        using var client = await AuthenticatedClient.CreateAsync(factory, "Staff");
        var group = await CreateGroupAsync(client, "Concurrency");
        var original = await CreateSessionAsync(client, group.Id, new(2026, 9, 15), new(9, 0), new(10, 0));
        var updated = await UpdateSessionAsync(client, original, new(2026, 9, 16), "Winner");
        Assert.NotEqual(original.RowVersion, updated.RowVersion);
        Assert.Equal(HttpStatusCode.Conflict, (await PutSessionAsync(client, original, new(2026, 9, 17))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await CommandResponseAsync(client, original, "complete")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await CommandResponseAsync(client, original, "cancel")).StatusCode);
        using var malformed = await client.PostAsJsonAsync($"/api/training-sessions/{updated.Id}/cancel", new { rowVersion = Convert.ToBase64String([1, 2]) });
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
    }

    [Fact]
    public async Task GroupDateUpdates_ProtectNonCancelledSessions()
    {
        using var client = await AuthenticatedClient.CreateAsync(factory, "Staff");
        var group = await CreateGroupAsync(client, "Range Guard");
        var scheduled = await CreateSessionAsync(client, group.Id, new(2026, 9, 5), new(9, 0), new(10, 0));
        Assert.Equal(HttpStatusCode.Conflict, (await UpdateGroupDatesAsync(client, group, new(2026, 9, 6), new(2026, 12, 1))).StatusCode);
        var persisted = await client.GetFromJsonAsync<GroupResponse>($"/api/training-groups/{group.Id}");
        Assert.Equal(new DateOnly(2026, 9, 1), persisted!.PlannedStartDate);
        await SessionCommandAsync(client, scheduled, "complete");
        Assert.Equal(HttpStatusCode.Conflict, (await UpdateGroupDatesAsync(client, persisted, new(2026, 9, 6), new(2026, 12, 1))).StatusCode);

        var cancelGroup = await CreateGroupAsync(client, "Cancelled Range");
        var cancelled = await CreateSessionAsync(client, cancelGroup.Id, new(2026, 9, 5), new(9, 0), new(10, 0));
        await SessionCommandAsync(client, cancelled, "cancel");
        using var allowed = await UpdateGroupDatesAsync(client, cancelGroup, new(2026, 9, 6), new(2026, 11, 30));
        allowed.EnsureSuccessStatusCode();

        var validGroup = await CreateGroupAsync(client, "Valid Range");
        await CreateSessionAsync(client, validGroup.Id, new(2026, 9, 10), new(9, 0), new(10, 0));
        using var valid = await UpdateGroupDatesAsync(client, validGroup, new(2026, 9, 5), new(2026, 11, 30));
        valid.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task EveryEndpoint_RejectsAnonymousAndInstructorWithoutMutation()
    {
        using var staff = await AuthenticatedClient.CreateAsync(factory, "Staff");
        var group = await CreateGroupAsync(staff, "Authorization");
        var session = await CreateSessionAsync(staff, group.Id, new(2026, 9, 15), new(9, 0), new(10, 0));
        using var anonymous = await AuthenticatedClient.CreateWithAntiforgeryAsync(factory,
            new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        using var instructor = await AuthenticatedClient.CreateAsync(factory, "Instructor");
        var requests = new Func<HttpClient, Task<HttpResponseMessage>>[]
        {
            c => PostSessionAsync(c, group.Id, new(2026, 9, 16)),
            c => c.GetAsync($"/api/training-groups/{group.Id}/sessions"),
            c => c.GetAsync($"/api/training-sessions/{session.Id}"),
            c => PutSessionAsync(c, session, new(2026, 9, 16)),
            c => CommandResponseAsync(c, session, "complete"),
            c => CommandResponseAsync(c, session, "cancel")
        };
        foreach (var request in requests)
        {
            using var a = await request(anonymous);
            using var i = await request(instructor);
            Assert.Equal(HttpStatusCode.Unauthorized, a.StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, i.StatusCode);
        }
        Assert.Equal("Scheduled", (await staff.GetFromJsonAsync<SessionResponse>($"/api/training-sessions/{session.Id}"))!.Status);
    }

    [Fact]
    public async Task Database_EnforcesTimeCheckAndRestrictedForeignKey()
    {
        using var client = await AuthenticatedClient.CreateAsync(factory, "Staff");
        var group = await CreateGroupAsync(client, "Database Rules");
        await CreateSessionAsync(client, group.Id, new(2026, 9, 15), new(9, 0), new(10, 0));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TcmdDbContext>();
        await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO TrainingSessions (Id, TrainingGroupId, SessionDate, StartTime, EndTime, Status, CreatedAtUtc, LastUpdatedAtUtc)
            VALUES ({Guid.NewGuid()}, {group.Id}, {new DateOnly(2026, 9, 16)}, {new TimeOnly(10, 0)}, {new TimeOnly(9, 0)}, {"Scheduled"}, {DateTimeOffset.UtcNow}, {DateTimeOffset.UtcNow})"));
        await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO TrainingSessions (Id, TrainingGroupId, SessionDate, StartTime, EndTime, Status, CreatedAtUtc, LastUpdatedAtUtc)
            VALUES ({Guid.NewGuid()}, {Guid.NewGuid()}, {new DateOnly(2026, 9, 16)}, {new TimeOnly(9, 0)}, {new TimeOnly(10, 0)}, {"Scheduled"}, {DateTimeOffset.UtcNow}, {DateTimeOffset.UtcNow})"));
        await Assert.ThrowsAsync<DbUpdateException>(async () =>
        {
            var tracked = await db.TrainingGroups.SingleAsync(x => x.Id == group.Id);
            db.TrainingGroups.Remove(tracked);
            await db.SaveChangesAsync();
        });
    }

    private static Task<HttpResponseMessage> PostSessionAsync(HttpClient client, Guid groupId, DateOnly date,
        TimeOnly? start = null, TimeOnly? end = null, string? location = null) =>
        client.PostAsJsonAsync($"/api/training-groups/{groupId}/sessions", new
        { sessionDate = date, startTime = start ?? new TimeOnly(9, 0), endTime = end ?? new TimeOnly(10, 0), location });

    private static async Task<SessionResponse> CreateSessionAsync(HttpClient client, Guid groupId, DateOnly date,
        TimeOnly start, TimeOnly end, string? location = null)
    {
        using var response = await PostSessionAsync(client, groupId, date, start, end, location);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var session = (await response.Content.ReadFromJsonAsync<SessionResponse>())!;
        Assert.Equal($"/api/training-sessions/{session.Id}", response.Headers.Location?.OriginalString);
        return session;
    }

    private static Task<HttpResponseMessage> PutSessionAsync(HttpClient client, SessionResponse session, DateOnly date,
        string? location = null) => client.PutAsJsonAsync($"/api/training-sessions/{session.Id}", new
        { sessionDate = date, startTime = new TimeOnly(10, 0), endTime = new TimeOnly(12, 0), location, rowVersion = session.RowVersion });

    private static async Task<SessionResponse> UpdateSessionAsync(HttpClient client, SessionResponse session,
        DateOnly date, string? location)
    {
        using var response = await PutSessionAsync(client, session, date, location);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SessionResponse>())!;
    }

    private static Task<HttpResponseMessage> CommandResponseAsync(HttpClient client, SessionResponse session, string command) =>
        client.PostAsJsonAsync($"/api/training-sessions/{session.Id}/{command}", new { rowVersion = session.RowVersion });

    private static async Task<SessionResponse> SessionCommandAsync(HttpClient client, SessionResponse session, string command)
    {
        using var response = await CommandResponseAsync(client, session, command);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SessionResponse>())!;
    }

    private static async Task<GroupResponse> CreateGroupAsync(HttpClient client, string name)
    {
        using var courseResponse = await client.PostAsJsonAsync("/api/courses", new { code = $"C-{Guid.NewGuid():N}", name = "Course" });
        var course = (await courseResponse.Content.ReadFromJsonAsync<CourseResponse>())!;
        using var instructorResponse = await client.PostAsJsonAsync("/api/instructors", new { fullName = $"Instructor {Guid.NewGuid():N}" });
        var instructor = (await instructorResponse.Content.ReadFromJsonAsync<InstructorResponse>())!;
        using var response = await client.PostAsJsonAsync("/api/training-groups", new
        { name, courseId = course.Id, primaryInstructorId = instructor.Id, plannedStartDate = new DateOnly(2026, 9, 1), plannedEndDate = new DateOnly(2026, 12, 1) });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<GroupResponse>())!;
    }

    private static async Task<GroupResponse> GroupCommandAsync(HttpClient client, GroupResponse group, string command)
    {
        using var response = await client.PostAsJsonAsync($"/api/training-groups/{group.Id}/{command}", new { rowVersion = group.RowVersion });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<GroupResponse>())!;
    }

    private static Task<HttpResponseMessage> UpdateGroupDatesAsync(HttpClient client, GroupResponse group,
        DateOnly start, DateOnly end) => client.PutAsJsonAsync($"/api/training-groups/{group.Id}", new
        { name = group.Name, courseId = group.CourseId, primaryInstructorId = group.PrimaryInstructorId, plannedStartDate = start, plannedEndDate = end, rowVersion = group.RowVersion });

    private sealed record CourseResponse(Guid Id);
    private sealed record InstructorResponse(Guid Id);
    private sealed record GroupResponse(Guid Id, string Name, Guid CourseId, Guid? PrimaryInstructorId,
        DateOnly PlannedStartDate, DateOnly PlannedEndDate, string Status, string RowVersion);
    private sealed record SessionResponse(Guid Id, Guid TrainingGroupId, DateOnly SessionDate, TimeOnly StartTime,
        TimeOnly EndTime, string Status, string? Location, DateTimeOffset CreatedAtUtc,
        DateTimeOffset LastUpdatedAtUtc, string RowVersion);
}
