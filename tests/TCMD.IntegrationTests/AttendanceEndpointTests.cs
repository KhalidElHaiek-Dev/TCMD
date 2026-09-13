using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TCMD.Domain.Attendance;
using TCMD.Infrastructure.Persistence;

namespace TCMD.IntegrationTests;

[Collection(DatabaseIntegrationCollection.Name)]
public sealed class AttendanceEndpointTests(TcmdApiFactory factory) : IAsyncLifetime
{
    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<TcmdDbContext>().AttendanceRecords.ExecuteDeleteAsync();
        factory.ResetUtcNow();
    }

    [Theory]
    [InlineData("Administrator", "Present")]
    [InlineData("Staff", "Absent")]
    [InlineData("Staff", "Late")]
    [InlineData("Staff", "Excused")]
    public async Task OperationalStaff_CanRecordEveryApprovedStatus(string role, string status)
    {
        SetStartedTime();
        using var client = await AuthenticatedClient.CreateAsync(factory, role);
        var setup = await CreateSetupAsync(client, $"Status {status}");
        using var response = await client.PostAsJsonAsync($"/api/training-sessions/{setup.Session.Id}/attendance",
            new { enrollmentId = setup.Enrollment.Id, status, createdByStaffUserId = Guid.NewGuid() });
        Assert.True(response.StatusCode == HttpStatusCode.Created,
            $"Expected Created but received {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        var result = (await response.Content.ReadFromJsonAsync<AttendanceResponse>())!;
        Assert.Equal(status, result.Status);
        Assert.Equal($"/api/attendance/{result.Id}", response.Headers.Location!.OriginalString);
        Assert.Equal(result.CreatedByStaffUserId, result.LastUpdatedByStaffUserId);
        Assert.Equal(8, Convert.FromBase64String(result.RowVersion).Length);
    }

    [Fact]
    public async Task Creation_EnforcesReferencesGroupStateDateAndUniqueness()
    {
        SetStartedTime();
        using var client = await AuthenticatedClient.CreateAsync(factory);
        var first = await CreateSetupAsync(client, "Rules A");
        var second = await CreateSetupAsync(client, "Rules B");
        Assert.Equal(HttpStatusCode.NotFound, (await PostAsync(client, first.Session.Id, Guid.NewGuid())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await PostAsync(client, Guid.NewGuid(), first.Enrollment.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await PostAsync(client, first.Session.Id, second.Enrollment.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync($"/api/training-sessions/{first.Session.Id}/attendance",
                new { enrollmentId = first.Enrollment.Id })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync($"/api/training-sessions/{first.Session.Id}/attendance",
                new { enrollmentId = first.Enrollment.Id, status = 99 })).StatusCode);
        await RecordAsync(client, first);
        Assert.Equal(HttpStatusCode.Conflict, (await PostAsync(client, first.Session.Id, first.Enrollment.Id)).StatusCode);

        var withdrawn = await CreateSetupAsync(client, "Withdrawn");
        await CommandAsync<EnrollmentResponse>(client, $"/api/enrollments/{withdrawn.Enrollment.Id}/withdraw",
            withdrawn.Enrollment.RowVersion);
        Assert.Equal(HttpStatusCode.Conflict, (await PostAsync(client, withdrawn.Session.Id, withdrawn.Enrollment.Id)).StatusCode);

        var completed = await CreateSetupAsync(client, "Completed enrollment");
        await CommandAsync<EnrollmentResponse>(client, $"/api/enrollments/{completed.Enrollment.Id}/complete",
            completed.Enrollment.RowVersion);
        Assert.Equal(HttpStatusCode.Conflict, (await PostAsync(client, completed.Session.Id, completed.Enrollment.Id)).StatusCode);

        var after = await CreateSetupAsync(client, "After date");
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TcmdDbContext>();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE Enrollments SET EnrollmentDate = {new DateOnly(2026, 9, 16)} WHERE Id = {after.Enrollment.Id}");
        Assert.Equal(HttpStatusCode.Conflict, (await PostAsync(client, after.Session.Id, after.Enrollment.Id)).StatusCode);
    }

    [Fact]
    public async Task ScheduledStartCompletedAndCancelledRulesUseDeterministicCasablancaTime()
    {
        factory.SetUtcNow(new(2026, 9, 15, 7, 59, 59, TimeSpan.Zero));
        using var client = await AuthenticatedClient.CreateAsync(factory);
        var scheduled = await CreateSetupAsync(client, "Clock");
        Assert.Equal(HttpStatusCode.Conflict, (await PostAsync(client, scheduled.Session.Id, scheduled.Enrollment.Id)).StatusCode);
        factory.SetUtcNow(new(2026, 9, 15, 8, 0, 0, TimeSpan.Zero));
        Assert.Equal(HttpStatusCode.Created, (await PostAsync(client, scheduled.Session.Id, scheduled.Enrollment.Id)).StatusCode);

        var completed = await CreateSetupAsync(client, "Completed session", new DateOnly(2026, 11, 1));
        await CommandAsync<SessionResponse>(client, $"/api/training-sessions/{completed.Session.Id}/complete",
            completed.Session.RowVersion);
        Assert.Equal(HttpStatusCode.Created, (await PostAsync(client, completed.Session.Id, completed.Enrollment.Id)).StatusCode);

        var cancelled = await CreateSetupAsync(client, "Cancelled session");
        await CommandAsync<SessionResponse>(client, $"/api/training-sessions/{cancelled.Session.Id}/cancel",
            cancelled.Session.RowVersion);
        Assert.Equal(HttpStatusCode.Conflict, (await PostAsync(client, cancelled.Session.Id, cancelled.Enrollment.Id)).StatusCode);
    }

    [Fact]
    public async Task CorrectionSupportsNotesNoOpConcurrencyAndHistoricalRecords()
    {
        SetStartedTime();
        using var client = await AuthenticatedClient.CreateAsync(factory);
        var setup = await CreateSetupAsync(client, "Corrections");
        var original = await RecordAsync(client, setup);
        var noOp = await CorrectAsync(client, original, "Present", "   ");
        Assert.Equal(original.RowVersion, noOp.RowVersion);
        Assert.Equal(original.LastUpdatedAtUtc, noOp.LastUpdatedAtUtc);
        Assert.Equal(original.LastUpdatedByStaffUserId, noOp.LastUpdatedByStaffUserId);

        await CommandAsync<EnrollmentResponse>(client, $"/api/enrollments/{setup.Enrollment.Id}/withdraw", setup.Enrollment.RowVersion);
        await CommandAsync<SessionResponse>(client, $"/api/training-sessions/{setup.Session.Id}/cancel", setup.Session.RowVersion);
        factory.SetUtcNow(new(2026, 9, 15, 13, 0, 0, TimeSpan.Zero));
        var corrected = await CorrectAsync(client, original, "Late", "  Register review  ");
        Assert.Equal(original.Id, corrected.Id);
        Assert.Equal("Register review", corrected.CorrectionNote);
        Assert.Equal(original.CreatedByStaffUserId, corrected.CreatedByStaffUserId);
        Assert.Equal(original.RecordedAtUtc, corrected.RecordedAtUtc);
        Assert.NotEqual(original.RowVersion, corrected.RowVersion);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PutAsJsonAsync($"/api/attendance/{original.Id}", new { rowVersion = corrected.RowVersion })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PutAsJsonAsync($"/api/attendance/{original.Id}", new { status = 99, rowVersion = corrected.RowVersion })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await client.PutAsJsonAsync($"/api/attendance/{original.Id}", new { status = "Absent", rowVersion = original.RowVersion })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PutAsJsonAsync($"/api/attendance/{original.Id}", new { status = "Absent", rowVersion = Convert.ToBase64String([1, 2]) })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PutAsJsonAsync($"/api/attendance/{original.Id}", new { status = "Absent", correctionNote = new string('x', 1001), rowVersion = corrected.RowVersion })).StatusCode);
        Assert.Equal(1000, (await CorrectAsync(client, corrected, "Excused", new string('x', 1000))).CorrectionNote!.Length);
    }

    [Fact]
    public async Task ViewsReturnRosterNullsAndSavedHistoricalAttendance()
    {
        SetStartedTime();
        using var client = await AuthenticatedClient.CreateAsync(factory);
        var setup = await CreateSetupAsync(client, "Views");
        var otherStudent = await PostAsync<StudentResponse>(client, "/api/students",
            new { fullName = $"Student {Guid.NewGuid():N}", phoneNumber = "123" });
        var otherEnrollment = await PostAsync<EnrollmentResponse>(client,
            $"/api/training-groups/{setup.GroupId}/enrollments", new { studentId = otherStudent.Id });
        var saved = await RecordAsync(client, setup, "Absent");
        var roster = (await client.GetFromJsonAsync<RosterResponse[]>($"/api/training-sessions/{setup.Session.Id}/attendance"))!;
        Assert.Contains(roster, x => x.EnrollmentId == otherEnrollment.Id && x.Attendance is null);
        Assert.Contains(roster, x => x.EnrollmentId == setup.Enrollment.Id && x.Attendance!.Id == saved.Id);
        Assert.Equal("Absent", roster.Single(x => x.EnrollmentId == setup.Enrollment.Id).Attendance!.Status);
        await CommandAsync<EnrollmentResponse>(client, $"/api/enrollments/{setup.Enrollment.Id}/withdraw", setup.Enrollment.RowVersion);
        Assert.Contains((await client.GetFromJsonAsync<RosterResponse[]>($"/api/training-sessions/{setup.Session.Id}/attendance"))!,
            x => x.EnrollmentId == setup.Enrollment.Id && x.Attendance is not null);
        Assert.Single((await client.GetFromJsonAsync<SavedViewResponse[]>($"/api/students/{setup.StudentId}/attendance"))!);
        Assert.Single((await client.GetFromJsonAsync<SavedViewResponse[]>($"/api/training-groups/{setup.GroupId}/attendance"))!);
        Assert.Empty((await client.GetFromJsonAsync<SavedViewResponse[]>($"/api/students/{otherStudent.Id}/attendance"))!);
        var empty = await CreateSetupAsync(client, "Empty saved views");
        Assert.Empty((await client.GetFromJsonAsync<SavedViewResponse[]>($"/api/training-groups/{empty.GroupId}/attendance"))!);
        await CommandAsync<EnrollmentResponse>(client, $"/api/enrollments/{empty.Enrollment.Id}/withdraw", empty.Enrollment.RowVersion);
        Assert.Empty((await client.GetFromJsonAsync<RosterResponse[]>($"/api/training-sessions/{empty.Session.Id}/attendance"))!);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/training-sessions/{Guid.NewGuid()}/attendance")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/students/{Guid.NewGuid()}/attendance")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/training-groups/{Guid.NewGuid()}/attendance")).StatusCode);
    }

    [Fact]
    public async Task DatabaseUniqueIndexRejectsDirectDuplicate()
    {
        SetStartedTime();
        using var client = await AuthenticatedClient.CreateAsync(factory);
        var setup = await CreateSetupAsync(client, "Database unique");
        var saved = await RecordAsync(client, setup);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TcmdDbContext>();
        db.AttendanceRecords.Add(AttendanceRecord.Create(setup.Enrollment.Id, setup.Session.Id,
            AttendanceStatus.Absent, saved.CreatedByStaffUserId, DateTimeOffset.UtcNow));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task ConcurrentCreationInactiveStudentAndDatabaseForeignKeysAreCovered()
    {
        SetStartedTime();
        using var client = await AuthenticatedClient.CreateAsync(factory);
        var setup = await CreateSetupAsync(client, "Concurrent and foreign keys");
        await CommandAsync<StudentResponse>(client, $"/api/students/{setup.StudentId}/deactivate", setup.StudentRowVersion);
        var attempts = await Task.WhenAll(PostAsync(client, setup.Session.Id, setup.Enrollment.Id),
            PostAsync(client, setup.Session.Id, setup.Enrollment.Id));
        Assert.Contains(attempts, x => x.StatusCode == HttpStatusCode.Created);
        Assert.Contains(attempts, x => x.StatusCode == HttpStatusCode.Conflict);
        var saved = (await client.GetFromJsonAsync<RosterResponse[]>($"/api/training-sessions/{setup.Session.Id}/attendance"))!
            .Single(x => x.EnrollmentId == setup.Enrollment.Id).Attendance!;
        var invalidCreator = await CreateSetupAsync(client, "Invalid creator FK");
        var invalidUpdater = await CreateSetupAsync(client, "Invalid updater FK");
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TcmdDbContext>();
        async Task InsertAsync(Guid enrollmentId, Guid sessionId, Guid creatorId, Guid updaterId) =>
            await db.Database.ExecuteSqlInterpolatedAsync($@"INSERT INTO AttendanceRecords
                (Id, EnrollmentId, TrainingSessionId, Status, CreatedByStaffUserId, LastUpdatedByStaffUserId, RecordedAtUtc, LastUpdatedAtUtc)
                VALUES ({Guid.NewGuid()}, {enrollmentId}, {sessionId}, {"Present"}, {creatorId}, {updaterId}, {DateTimeOffset.UtcNow}, {DateTimeOffset.UtcNow})");
        await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() =>
            InsertAsync(Guid.NewGuid(), setup.Session.Id, saved.CreatedByStaffUserId, saved.CreatedByStaffUserId));
        await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() =>
            InsertAsync(setup.Enrollment.Id, Guid.NewGuid(), saved.CreatedByStaffUserId, saved.CreatedByStaffUserId));
        await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() =>
            InsertAsync(invalidCreator.Enrollment.Id, invalidCreator.Session.Id, Guid.NewGuid(), saved.CreatedByStaffUserId));
        await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() =>
            InsertAsync(invalidUpdater.Enrollment.Id, invalidUpdater.Session.Id, saved.CreatedByStaffUserId, Guid.NewGuid()));
    }

    [Fact]
    public async Task EveryEndpointRejectsAnonymousAndInstructor()
    {
        SetStartedTime();
        using var staff = await AuthenticatedClient.CreateAsync(factory);
        var setup = await CreateSetupAsync(staff, "Authorization");
        var attendance = await RecordAsync(staff, setup);
        using var anonymous = await AuthenticatedClient.CreateWithAntiforgeryAsync(factory,
            new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        using var instructor = await AuthenticatedClient.CreateAsync(factory, "Instructor");
        Func<HttpClient, Task<HttpResponseMessage>>[] calls =
        [
            c => PostAsync(c, setup.Session.Id, setup.Enrollment.Id),
            c => c.GetAsync($"/api/training-sessions/{setup.Session.Id}/attendance"),
            c => c.GetAsync($"/api/students/{setup.StudentId}/attendance"),
            c => c.GetAsync($"/api/training-groups/{setup.GroupId}/attendance"),
            c => c.PutAsJsonAsync($"/api/attendance/{attendance.Id}", new { status = "Late", rowVersion = attendance.RowVersion })
        ];
        foreach (var call in calls)
        {
            using var a = await call(anonymous);
            using var i = await call(instructor);
            Assert.Equal(HttpStatusCode.Unauthorized, a.StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, i.StatusCode);
        }
        var unchanged = (await staff.GetFromJsonAsync<RosterResponse[]>($"/api/training-sessions/{setup.Session.Id}/attendance"))!
            .Single(x => x.EnrollmentId == setup.Enrollment.Id).Attendance!;
        Assert.Equal(attendance.Status, unchanged.Status);
        Assert.Equal(attendance.RowVersion, unchanged.RowVersion);
    }

    private void SetStartedTime() => factory.SetUtcNow(new(2026, 9, 15, 12, 0, 0, TimeSpan.Zero));
    private static Task<HttpResponseMessage> PostAsync(HttpClient client, Guid sessionId, Guid enrollmentId) =>
        client.PostAsJsonAsync($"/api/training-sessions/{sessionId}/attendance", new { enrollmentId, status = "Present" });
    private static async Task<AttendanceResponse> RecordAsync(HttpClient client, Setup setup, string status = "Present")
    {
        using var response = await client.PostAsJsonAsync($"/api/training-sessions/{setup.Session.Id}/attendance",
            new { enrollmentId = setup.Enrollment.Id, status });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AttendanceResponse>())!;
    }
    private static async Task<AttendanceResponse> CorrectAsync(HttpClient client, AttendanceResponse current,
        string status, string? note) => await PutAsync<AttendanceResponse>(client, $"/api/attendance/{current.Id}",
            new { status, correctionNote = note, rowVersion = current.RowVersion });

    private static async Task<T> PostAsync<T>(HttpClient client, string path, object body)
    {
        using var response = await client.PostAsJsonAsync(path, body);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
    private static async Task<T> PutAsync<T>(HttpClient client, string path, object body)
    {
        using var response = await client.PutAsJsonAsync(path, body);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
    private static Task<T> CommandAsync<T>(HttpClient client, string path, string rowVersion) =>
        PostAsync<T>(client, path, new { rowVersion });

    private static async Task<Setup> CreateSetupAsync(HttpClient client, string name, DateOnly? sessionDate = null)
    {
        var course = await PostAsync<IdResponse>(client, "/api/courses", new { code = $"A-{Guid.NewGuid():N}", name = "Course" });
        var instructor = await PostAsync<IdResponse>(client, "/api/instructors", new { fullName = $"Instructor {Guid.NewGuid():N}" });
        var group = await PostAsync<IdResponse>(client, "/api/training-groups", new
        { name, courseId = course.Id, primaryInstructorId = instructor.Id, plannedStartDate = new DateOnly(2026, 9, 1), plannedEndDate = new DateOnly(2026, 12, 1) });
        var student = await PostAsync<StudentResponse>(client, "/api/students", new { fullName = $"Student {Guid.NewGuid():N}", phoneNumber = "123" });
        var enrollment = await PostAsync<EnrollmentResponse>(client, $"/api/training-groups/{group.Id}/enrollments", new { studentId = student.Id });
        var session = await PostAsync<SessionResponse>(client, $"/api/training-groups/{group.Id}/sessions", new
        { sessionDate = sessionDate ?? new DateOnly(2026, 9, 15), startTime = new TimeOnly(9, 0), endTime = new TimeOnly(10, 0) });
        return new(group.Id, student.Id, student.RowVersion, enrollment, session);
    }

    private sealed record Setup(Guid GroupId, Guid StudentId, string StudentRowVersion,
        EnrollmentResponse Enrollment, SessionResponse Session);
    private sealed record IdResponse(Guid Id);
    private sealed record StudentResponse(Guid Id, string RowVersion);
    private sealed record EnrollmentResponse(Guid Id, string RowVersion);
    private sealed record SessionResponse(Guid Id, string RowVersion);
    private sealed record AttendanceResponse(Guid Id, string Status, string? CorrectionNote,
        Guid CreatedByStaffUserId, Guid LastUpdatedByStaffUserId, DateTimeOffset RecordedAtUtc,
        DateTimeOffset LastUpdatedAtUtc, string RowVersion);
    private sealed record RosterResponse(Guid EnrollmentId, AttendanceResponse? Attendance);
    private sealed record SavedViewResponse(AttendanceResponse Attendance);
}
