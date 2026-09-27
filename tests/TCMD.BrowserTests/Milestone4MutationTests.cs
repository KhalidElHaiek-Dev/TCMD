using System.Text.Json;
using Microsoft.Playwright;

namespace TCMD.BrowserTests;

[Collection(BrowserCollection.Name)]
public sealed class Milestone4MutationTests(BrowserFixture fixture)
{
    [Theory]
    [InlineData(320)]
    [InlineData(375)]
    [InlineData(768)]
    public async Task Attendance_SavePreservesSiblingInput_UsesServerState_AndPreventsDuplicates(int viewportWidth)
    {
        await using var context = await SignInAsync(viewportWidth);
        var seed = await CreateAttendanceSeedAsync(context, twoStudents: true);
        var page = context.Pages.Single();
        await page.GotoAsync($"{fixture.BaseUrl}/#/sessions/{seed.SessionId}");
        await MarkDocumentAsync(page);

        var first = AttendanceRow(page, seed.FirstStudentName);
        var second = AttendanceRow(page, seed.SecondStudentName!);
        await first.GetByLabel($"Attendance status for {seed.FirstStudentName}").SelectOptionAsync("Excused");
        await first.GetByLabel($"Attendance correction note for {seed.FirstStudentName}").FillAsync("saved note");
        await second.GetByLabel($"Attendance status for {seed.SecondStudentName}").SelectOptionAsync("Absent");
        await second.GetByLabel($"Attendance correction note for {seed.SecondStudentName}").FillAsync("unsaved sibling note");

        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var writes = 0;
        await page.RouteAsync($"**/api/attendance/{seed.FirstAttendanceId}", async route =>
        {
            Interlocked.Increment(ref writes);
            await gate.Task;
            var response = await route.FetchAsync();
            await route.FulfillAsync(new() { Response = response });
        });

        var button = first.GetByRole(AriaRole.Button, new() { Name = $"Correct attendance for {seed.FirstStudentName}", Exact = true });
        await button.EvaluateAsync("button => { button.click(); button.click(); }");
        await Assertions.Expect(button).ToBeDisabledAsync();
        Assert.Equal(1, Volatile.Read(ref writes));
        gate.SetResult();
        await Assertions.Expect(page.GetByRole(AriaRole.Status).Filter(new() { HasText = "Attendance saved." })).ToBeVisibleAsync();

        Assert.Equal(1, Volatile.Read(ref writes));
        await AssertSameDocumentAsync(page);
        await Assertions.Expect(second.GetByLabel($"Attendance status for {seed.SecondStudentName}")).ToHaveValueAsync("Absent");
        await Assertions.Expect(second.GetByLabel($"Attendance correction note for {seed.SecondStudentName}")).ToHaveValueAsync("unsaved sibling note");
        var saved = (await ApiAsync(context, $"/api/training-sessions/{seed.SessionId}/attendance"))
            .EnumerateArray().Single(x => x.GetProperty("attendance").GetProperty("id").GetString() == seed.FirstAttendanceId)
            .GetProperty("attendance");
        await Assertions.Expect(first.GetByLabel($"Attendance status for {seed.FirstStudentName}")).ToHaveValueAsync(saved.GetProperty("status").GetString()!);
        await Assertions.Expect(first.GetByLabel($"Attendance correction note for {seed.FirstStudentName}")).ToHaveValueAsync(saved.GetProperty("correctionNote").GetString()!);
        await Assertions.Expect(first.GetByRole(AriaRole.Button, new() { Name = $"Correct attendance for {seed.FirstStudentName}", Exact = true })).ToBeEnabledAsync();
    }

    [Fact]
    public async Task Attendance_FailurePreservesInput_RestoresControl_AndDoesNotRetry()
    {
        await using var context = await SignInAsync();
        var seed = await CreateAttendanceSeedAsync(context, twoStudents: false);
        var page = context.Pages.Single();
        await page.GotoAsync($"{fixture.BaseUrl}/#/sessions/{seed.SessionId}");
        await MarkDocumentAsync(page);
        var row = AttendanceRow(page, seed.FirstStudentName);
        await row.GetByLabel($"Attendance status for {seed.FirstStudentName}").SelectOptionAsync("Late");
        await row.GetByLabel($"Attendance correction note for {seed.FirstStudentName}").FillAsync("keep this correction");
        var writes = 0;
        await page.RouteAsync($"**/api/attendance/{seed.FirstAttendanceId}", async route =>
        {
            Interlocked.Increment(ref writes);
            await route.FulfillAsync(new() { Status = 500, ContentType = "application/problem+json", Body = "{\"title\":\"Controlled attendance failure\"}" });
        });

        var button = row.GetByRole(AriaRole.Button, new() { Name = $"Correct attendance for {seed.FirstStudentName}", Exact = true });
        await button.ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Alert).Filter(new() { HasText = "Controlled attendance failure" })).ToBeVisibleAsync();
        await Assertions.Expect(button).ToBeEnabledAsync();
        await Assertions.Expect(row.GetByLabel($"Attendance status for {seed.FirstStudentName}")).ToHaveValueAsync("Late");
        await Assertions.Expect(row.GetByLabel($"Attendance correction note for {seed.FirstStudentName}")).ToHaveValueAsync("keep this correction");
        await AssertSameDocumentAsync(page);
        await page.WaitForTimeoutAsync(100);
        Assert.Equal(1, writes);
    }

    [Fact]
    public async Task Enrollment_MutationsAreLocal_Deduplicated_AndRecoverAfterFailure()
    {
        await using var context = await SignInAsync();
        var course = await CreateCourseAsync(context);
        var group = await CreateGroupAsync(context, Id(course));
        var first = await CreateStudentAsync(context, "Enrollment first");
        var second = await CreateStudentAsync(context, "Enrollment second");
        var page = context.Pages.Single();
        await page.GotoAsync($"{fixture.BaseUrl}/#/groups/{Id(group)}?tab=enrollments");
        await page.GetByLabel("Group name").FillAsync("unrelated unsaved group name");
        await MarkDocumentAsync(page);

        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var writes = 0;
        await page.RouteAsync($"**/api/training-groups/{Id(group)}/enrollments", async route =>
        {
            if (route.Request.Method != "POST") { await route.ContinueAsync(); return; }
            Interlocked.Increment(ref writes);
            await gate.Task;
            var response = await route.FetchAsync();
            await route.FulfillAsync(new() { Response = response });
        });
        await page.GetByLabel("Active Student").SelectOptionAsync(Id(first));
        var submit = page.GetByRole(AriaRole.Button, new() { Name = "Enroll Student", Exact = true });
        await submit.EvaluateAsync("button => { button.click(); button.click(); }");
        await Assertions.Expect(submit).ToBeDisabledAsync();
        Assert.Equal(1, writes);
        gate.SetResult();
        await Assertions.Expect(page.GetByRole(AriaRole.Status).Filter(new() { HasText = "Student enrolled." })).ToBeVisibleAsync();
        await AssertSameDocumentAsync(page);
        await Assertions.Expect(page.GetByLabel("Group name")).ToHaveValueAsync("unrelated unsaved group name");
        Assert.Equal(1, writes);

        await page.UnrouteAsync($"**/api/training-groups/{Id(group)}/enrollments");
        writes = 0;
        await page.RouteAsync($"**/api/training-groups/{Id(group)}/enrollments", async route =>
        {
            if (route.Request.Method != "POST") { await route.ContinueAsync(); return; }
            Interlocked.Increment(ref writes);
            await route.FulfillAsync(new() { Status = 409, ContentType = "application/problem+json", Body = "{\"title\":\"Controlled enrollment failure\"}" });
        });
        await page.GetByLabel("Active Student").SelectOptionAsync(Id(second));
        submit = page.GetByRole(AriaRole.Button, new() { Name = "Enroll Student", Exact = true });
        await submit.ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Alert).Filter(new() { HasText = "Controlled enrollment failure" })).ToBeVisibleAsync();
        await Assertions.Expect(submit).ToBeEnabledAsync();
        await Assertions.Expect(page.GetByLabel("Active Student")).ToHaveValueAsync(Id(second));
        await page.WaitForTimeoutAsync(100);
        Assert.Equal(1, writes);
    }

    [Fact]
    public async Task Enrollment_StudentLookupFailureIsVisible()
    {
        await using var context = await SignInAsync();
        var course = await CreateCourseAsync(context);
        var group = await CreateGroupAsync(context, Id(course));
        var page = context.Pages.Single();
        await page.RouteAsync("**/api/students?isActive=true", route => route.FulfillAsync(new()
        {
            Status = 503, ContentType = "application/problem+json", Body = "{\"title\":\"Student lookup unavailable\"}"
        }));
        await page.GotoAsync($"{fixture.BaseUrl}/#/groups/{Id(group)}?tab=enrollments");
        await Assertions.Expect(page.GetByRole(AriaRole.Alert).Filter(new() { HasText = "Student lookup unavailable" })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Enrollments", Exact = true })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task InstructorLink_IsLocal_Deduplicated_AndLookupFailureIsVisible()
    {
        await using var context = await SignInAsync();
        var instructor = await ApiAsync(context, "/api/instructors", "POST", new { fullName = $"Link {Guid.NewGuid():N}" }, 201);
        var account = await ApiAsync(context, "/api/staff-accounts", "POST", new
        {
            userName = $"link-{Guid.NewGuid():N}", displayName = "Link account", password = "Password1", role = "Instructor"
        }, 201);
        var page = context.Pages.Single();
        await page.GotoAsync($"{fixture.BaseUrl}/#/staff-accounts/{Id(account)}");
        await MarkDocumentAsync(page);
        await page.GetByLabel("Linked Instructor").SelectOptionAsync(Id(instructor));
        var writes = 0;
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await page.RouteAsync($"**/api/staff-accounts/{Id(account)}/instructor-link", async route =>
        {
            Interlocked.Increment(ref writes);
            await gate.Task;
            var response = await route.FetchAsync();
            await route.FulfillAsync(new() { Response = response });
        });
        await page.GetByRole(AriaRole.Button, new() { Name = "Update link", Exact = true }).ClickAsync();
        var confirm = page.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Button, new() { Name = "Confirm", Exact = true });
        await confirm.EvaluateAsync("button => { button.click(); button.click(); }");
        var linkButton = page.GetByRole(AriaRole.Button, new() { Name = "Update link", Exact = true });
        await Assertions.Expect(linkButton).ToBeDisabledAsync();
        Assert.Equal(1, writes);
        gate.SetResult();
        await Assertions.Expect(page.GetByRole(AriaRole.Status).Filter(new() { HasText = "Instructor link updated." })).ToBeVisibleAsync();
        await AssertSameDocumentAsync(page);
        Assert.Equal(1, writes);
        await Assertions.Expect(page.GetByLabel("Linked Instructor")).ToHaveValueAsync(Id(instructor));

        await page.UnrouteAsync($"**/api/staff-accounts/{Id(account)}/instructor-link");
        await page.RouteAsync("**/api/instructors?isActive=true", route => route.FulfillAsync(new()
        {
            Status = 503, ContentType = "application/problem+json", Body = "{\"title\":\"Instructor lookup unavailable\"}"
        }));
        await page.ReloadAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Alert).Filter(new() { HasText = "Instructor lookup unavailable" })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Change role", Exact = true })).ToBeVisibleAsync();
    }

    [Theory]
    [InlineData("students", "Student")]
    [InlineData("instructors", "Instructor")]
    [InlineData("courses", "Course")]
    public async Task EntityDeactivation_UpdatesLocally(string kind, string singular)
    {
        await using var context = await SignInAsync();
        JsonElement entity = kind switch
        {
            "students" => await CreateStudentAsync(context, "Deactivate student"),
            "instructors" => await ApiAsync(context, "/api/instructors", "POST", new { fullName = $"Deactivate {Guid.NewGuid():N}" }, 201),
            _ => await CreateCourseAsync(context)
        };
        var page = context.Pages.Single();
        await page.GotoAsync($"{fixture.BaseUrl}/#/{kind}/{Id(entity)}");
        await MarkDocumentAsync(page);
        await page.GetByRole(AriaRole.Button, new() { Name = "Deactivate", Exact = true }).ClickAsync();
        await page.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Button, new() { Name = "Deactivate", Exact = true }).ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Status).Filter(new() { HasText = $"{singular} deactivated." })).ToBeVisibleAsync();
        var status = kind == "students"
            ? page.Locator(".entity-header").GetByText("Inactive", new() { Exact = true })
            : page.GetByText("Inactive", new() { Exact = true });
        await Assertions.Expect(status).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Deactivate", Exact = true })).ToHaveCountAsync(0);
        await AssertSameDocumentAsync(page);
    }

    private async Task<IBrowserContext> SignInAsync(int viewportWidth = 1280)
    {
        var context = await fixture.Browser.NewContextAsync(new()
        {
            IgnoreHTTPSErrors = true,
            ViewportSize = new() { Width = viewportWidth, Height = 800 }
        });
        await ApiAsync(context, "/api/auth/login", "POST", new { userName = BrowserFixture.AdminUserName, password = BrowserFixture.AdminPassword }, 204);
        await context.NewPageAsync();
        return context;
    }

    private async Task<AttendanceSeed> CreateAttendanceSeedAsync(IBrowserContext context, bool twoStudents)
    {
        var course = await CreateCourseAsync(context);
        var group = await CreateGroupAsync(context, Id(course));
        var first = await CreateStudentAsync(context, "Attendance first");
        var firstEnrollment = await ApiAsync(context, $"/api/training-groups/{Id(group)}/enrollments", "POST", new { studentId = Id(first) }, 201);
        JsonElement second = default, secondEnrollment = default;
        if (twoStudents)
        {
            second = await CreateStudentAsync(context, "Attendance second");
            secondEnrollment = await ApiAsync(context, $"/api/training-groups/{Id(group)}/enrollments", "POST", new { studentId = Id(second) }, 201);
        }
        var session = await ApiAsync(context, $"/api/training-groups/{Id(group)}/sessions", "POST", new
        {
            sessionDate = group.GetProperty("plannedStartDate").GetString(), startTime = "09:00", endTime = "10:00"
        }, 201);
        session = await ApiAsync(context, $"/api/training-sessions/{Id(session)}/complete", "POST", new { rowVersion = Version(session) });
        var firstAttendance = await ApiAsync(context, $"/api/training-sessions/{Id(session)}/attendance", "POST", new { enrollmentId = Id(firstEnrollment), status = "Present" }, 201);
        if (twoStudents)
            await ApiAsync(context, $"/api/training-sessions/{Id(session)}/attendance", "POST", new { enrollmentId = Id(secondEnrollment), status = "Present" }, 201);
        return new(Id(session), Id(firstAttendance), first.GetProperty("fullName").GetString()!, twoStudents ? second.GetProperty("fullName").GetString() : null);
    }

    private Task<JsonElement> CreateCourseAsync(IBrowserContext context) => ApiAsync(context, "/api/courses", "POST", new
    {
        code = $"M{Guid.NewGuid():N}"[..20], name = "Milestone 4 course", description = ""
    }, 201);

    private Task<JsonElement> CreateStudentAsync(IBrowserContext context, string prefix) =>
        ApiAsync(context, "/api/students", "POST", new { fullName = $"{prefix} {Guid.NewGuid():N}", phoneNumber = "+212600000001" }, 201);

    private Task<JsonElement> CreateGroupAsync(IBrowserContext context, string courseId) => ApiAsync(context, "/api/training-groups", "POST", new
    {
        name = $"Milestone group {Guid.NewGuid():N}", courseId, plannedStartDate = DateTime.UtcNow.ToString("yyyy-MM-dd"), plannedEndDate = DateTime.UtcNow.AddDays(2).ToString("yyyy-MM-dd")
    }, 201);

    private async Task<JsonElement> ApiAsync(IBrowserContext context, string path, string method = "GET", object? body = null, int expectedStatus = 200)
    {
        var headers = new Dictionary<string, string>();
        if (method != "GET")
        {
            var security = await context.APIRequest.GetAsync($"{fixture.BaseUrl}/api/auth/antiforgery");
            using var tokens = JsonDocument.Parse(await security.TextAsync());
            headers["X-CSRF-TOKEN"] = tokens.RootElement.GetProperty("requestToken").GetString()!;
            await security.DisposeAsync();
        }
        var response = await context.APIRequest.FetchAsync($"{fixture.BaseUrl}{path}", new() { Method = method, DataObject = body, Headers = headers });
        var content = await response.TextAsync();
        Assert.True(response.Status == expectedStatus, $"{method} {path}: expected {expectedStatus}, got {response.Status}: {content}");
        await response.DisposeAsync();
        if (expectedStatus == 204) return default;
        using var document = JsonDocument.Parse(content);
        return document.RootElement.Clone();
    }

    private static ILocator AttendanceRow(IPage page, string studentName) => page.Locator("tbody tr").Filter(new() { HasText = studentName });
    private static Task MarkDocumentAsync(IPage page) => page.EvaluateAsync("window.milestone4Document = 'original'");
    private static async Task AssertSameDocumentAsync(IPage page) => Assert.Equal("original", await page.EvaluateAsync<string?>("window.milestone4Document"));
    private static string Id(JsonElement item) => item.GetProperty("id").GetString()!;
    private static string Version(JsonElement item) => item.GetProperty("rowVersion").GetString()!;
    private sealed record AttendanceSeed(string SessionId, string FirstAttendanceId, string FirstStudentName, string? SecondStudentName);
}
