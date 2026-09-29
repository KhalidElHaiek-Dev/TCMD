using System.Text.Json;
using Microsoft.Playwright;

namespace TCMD.BrowserTests;

[Collection(BrowserCollection.Name)]
public sealed class FormDataPreservationTests(BrowserFixture fixture)
{
    [Fact]
    public async Task CourseEdit_LoadsAndPreservesDescription_WhenNameChanges()
    {
        await using var context = await SignInAsync();
        const string description = "First line\n<b>Plain text & details</b>";
        var course = await CreateCourseAsync(context, description);
        var id = Id(course);
        var page = context.Pages.Single();
        await page.GotoAsync($"{fixture.BaseUrl}/#/courses/{id}");
        await Assertions.Expect(page.GetByLabel("Description")).ToHaveValueAsync(description);
        await page.GetByLabel("Name", new() { Exact = true }).FillAsync("Updated course name");
        await SaveAsync(page, "Save changes", $"/api/courses/{id}");
        var saved = await ApiAsync(context, $"/api/courses/{id}");
        Assert.Equal("Updated course name", saved.GetProperty("name").GetString());
        Assert.Equal(description, saved.GetProperty("description").GetString());
        await page.ReloadAsync();
        await Assertions.Expect(page.GetByLabel("Description")).ToHaveValueAsync(description);
    }

    [Fact]
    public async Task AttendanceCorrection_LoadsAndPreservesNote_WhenStatusChanges()
    {
        await using var context = await SignInAsync();
        var course = await CreateCourseAsync(context);
        var group = await CreateGroupAsync(context, Id(course), null);
        var student = await ApiAsync(context, "/api/students", "POST",
            new { fullName = "Note preservation student", phoneNumber = "+212600000001" }, 201);
        var enrollment = await ApiAsync(context, $"/api/training-groups/{Id(group)}/enrollments", "POST",
            new { studentId = Id(student) }, 201);
        // A Completed session accepts entry without relying on the browser host's timezone or clock boundary.
        var session = await ApiAsync(context, $"/api/training-groups/{Id(group)}/sessions", "POST",
            new { sessionDate = group.GetProperty("plannedStartDate").GetString(), startTime = "09:00", endTime = "10:00" }, 201);
        await ApiAsync(context, $"/api/training-sessions/{Id(session)}/complete", "POST",
            new { rowVersion = Version(session) });
        var attendance = await ApiAsync(context, $"/api/training-sessions/{Id(session)}/attendance", "POST",
            new { enrollmentId = Id(enrollment), status = "Present" }, 201);
        const string note = "Existing explanation\n<plain text> & details";
        await ApiAsync(context, $"/api/attendance/{Id(attendance)}", "PUT",
            new { status = "Late", correctionNote = note, rowVersion = Version(attendance) });

        var page = context.Pages.Single();
        await page.GotoAsync($"{fixture.BaseUrl}/#/sessions/{Id(session)}");
        await Assertions.Expect(page.GetByLabel("Attendance correction note for Note preservation student", new() { Exact = true })).ToHaveValueAsync(note);
        await page.GetByLabel("Attendance status for Note preservation student", new() { Exact = true }).SelectOptionAsync("Excused");
        await SaveAsync(page, "Correct attendance for Note preservation student", $"/api/attendance/{Id(attendance)}");
        var roster = await ApiAsync(context, $"/api/training-sessions/{Id(session)}/attendance");
        var saved = roster.EnumerateArray().Single().GetProperty("attendance");
        Assert.Equal("Excused", saved.GetProperty("status").GetString());
        Assert.Equal(note, saved.GetProperty("correctionNote").GetString());
        await page.ReloadAsync();
        await Assertions.Expect(page.GetByLabel("Attendance correction note for Note preservation student", new() { Exact = true })).ToHaveValueAsync(note);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GroupEdit_PreservesInactiveReferences_WithoutOfferingNewInactiveAssignments(bool active)
    {
        await using var context = await SignInAsync();
        var course = await CreateCourseAsync(context);
        var instructor = await CreateInstructorAsync(context);
        var group = await CreateGroupAsync(context, Id(course), Id(instructor));
        if (active)
            await ApiAsync(context, $"/api/training-groups/{Id(group)}/activate", "POST", new { rowVersion = Version(group) });
        await DeactivateAsync(context, "courses", course);
        await DeactivateAsync(context, "instructors", instructor);
        var otherCourse = await CreateCourseAsync(context);
        var otherInstructor = await CreateInstructorAsync(context);
        await DeactivateAsync(context, "courses", otherCourse);
        await DeactivateAsync(context, "instructors", otherInstructor);

        var page = context.Pages.Single();
        await page.GotoAsync($"{fixture.BaseUrl}/#/groups/{Id(group)}");
        await page.GetByRole(AriaRole.Button, new() { Name = "Edit details", Exact = true }).ClickAsync();
        await ExpectCurrentInactiveAsync(page.GetByLabel("Course", new() { Exact = true }), Id(course));
        await ExpectCurrentInactiveAsync(page.GetByLabel("Primary Instructor"), Id(instructor));
        await ExpectMissingOptionAsync(page.GetByLabel("Course", new() { Exact = true }), Id(otherCourse));
        await ExpectMissingOptionAsync(page.GetByLabel("Primary Instructor"), Id(otherInstructor));
        await page.GetByLabel("Group name").FillAsync("Preserved historical references");
        await SaveAsync(page, "Save changes", $"/api/training-groups/{Id(group)}");
        var saved = await ApiAsync(context, $"/api/training-groups/{Id(group)}");
        Assert.Equal("Preserved historical references", saved.GetProperty("name").GetString());
        Assert.Equal(Id(course), saved.GetProperty("courseId").GetString());
        Assert.Equal(Id(instructor), saved.GetProperty("primaryInstructorId").GetString());

        // A Planned group permits intentional unassignment and Course reassignment.
        if (!active)
        {
            var replacementCourse = await CreateCourseAsync(context);
            await page.ReloadAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "Edit details", Exact = true }).ClickAsync();
            await page.GetByLabel("Course", new() { Exact = true }).SelectOptionAsync(Id(replacementCourse));
            await page.GetByLabel("Primary Instructor").SelectOptionAsync("");
            await SaveAsync(page, "Save changes", $"/api/training-groups/{Id(group)}");
            saved = await ApiAsync(context, $"/api/training-groups/{Id(group)}");
            Assert.Equal(Id(replacementCourse), saved.GetProperty("courseId").GetString());
            Assert.Equal(JsonValueKind.Null, saved.GetProperty("primaryInstructorId").ValueKind);
        }

        var replacementInstructor = await CreateInstructorAsync(context);
        await page.ReloadAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Edit details", Exact = true }).ClickAsync();
        await page.GetByLabel("Primary Instructor").SelectOptionAsync(Id(replacementInstructor));
        await SaveAsync(page, "Save changes", $"/api/training-groups/{Id(group)}");
        saved = await ApiAsync(context, $"/api/training-groups/{Id(group)}");
        Assert.Equal(Id(replacementInstructor), saved.GetProperty("primaryInstructorId").GetString());
        await page.ReloadAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Edit details", Exact = true }).ClickAsync();
        await Assertions.Expect(page.GetByLabel("Primary Instructor")).ToHaveValueAsync(Id(replacementInstructor));
        await ExpectMissingOptionAsync(page.GetByLabel("Primary Instructor"), Id(instructor));

        await page.GotoAsync($"{fixture.BaseUrl}/#/groups/new");
        await Assertions.Expect(page.GetByLabel("Group name")).ToBeVisibleAsync();
        await ExpectMissingOptionAsync(page.GetByLabel("Course", new() { Exact = true }), Id(course));
        await ExpectMissingOptionAsync(page.GetByLabel("Primary Instructor"), Id(instructor));
    }

    [Fact]
    public async Task AccountLink_PreservesInactiveCurrentLink_AndAllowsIntentionalUnlinkAndReassignment()
    {
        await using var context = await SignInAsync();
        var instructor = await CreateInstructorAsync(context);
        var otherInactive = await CreateInstructorAsync(context);
        var account = await ApiAsync(context, "/api/staff-accounts", "POST",
            new { userName = $"preserve-{Guid.NewGuid():N}", displayName = "Preserved link", password = "Password1", role = "Instructor" }, 201);
        var accountPath = $"/api/staff-accounts/{Id(account)}";
        await ApiAsync(context, $"{accountPath}/instructor-link", "PUT", new { instructorId = Id(instructor) });
        await DeactivateAsync(context, "instructors", instructor);
        await DeactivateAsync(context, "instructors", otherInactive);
        var page = context.Pages.Single();
        await page.GotoAsync($"{fixture.BaseUrl}/#/staff-accounts/{Id(account)}");
        await ExpectCurrentInactiveAsync(page.GetByLabel("Linked Instructor"), Id(instructor));
        await ExpectMissingOptionAsync(page.GetByLabel("Linked Instructor"), Id(otherInactive));
        await ConfirmSaveAsync(page, "Update link", $"{accountPath}/instructor-link");
        Assert.Equal(Id(instructor), (await ApiAsync(context, accountPath)).GetProperty("instructorId").GetString());
        await page.ReloadAsync();
        await ExpectCurrentInactiveAsync(page.GetByLabel("Linked Instructor"), Id(instructor));

        // Changing unrelated account state must leave the existing inactive link intact.
        await ConfirmSaveAsync(page, "Deactivate account", $"{accountPath}/active");
        Assert.Equal(Id(instructor), (await ApiAsync(context, accountPath)).GetProperty("instructorId").GetString());
        await page.ReloadAsync();
        await ExpectCurrentInactiveAsync(page.GetByLabel("Linked Instructor"), Id(instructor));
        await page.GetByLabel("Linked Instructor").SelectOptionAsync("");
        await ConfirmSaveAsync(page, "Update link", $"{accountPath}/instructor-link");
        Assert.Equal(JsonValueKind.Null, (await ApiAsync(context, accountPath)).GetProperty("instructorId").ValueKind);
        await page.ReloadAsync();
        await Assertions.Expect(page.GetByLabel("Linked Instructor")).ToHaveValueAsync("");
        await ExpectMissingOptionAsync(page.GetByLabel("Linked Instructor"), Id(instructor));
        await ApiAsync(context, $"{accountPath}/instructor-link", "PUT", new { instructorId = Id(instructor) }, 409);

        var replacement = await CreateInstructorAsync(context);
        await page.ReloadAsync();
        await page.GetByLabel("Linked Instructor").SelectOptionAsync(Id(replacement));
        await ConfirmSaveAsync(page, "Update link", $"{accountPath}/instructor-link");
        Assert.Equal(Id(replacement), (await ApiAsync(context, accountPath)).GetProperty("instructorId").GetString());
        await page.ReloadAsync();
        await Assertions.Expect(page.GetByLabel("Linked Instructor")).ToHaveValueAsync(Id(replacement));
    }

    private async Task<IBrowserContext> SignInAsync()
    {
        var context = await fixture.Browser.NewContextAsync(new() { IgnoreHTTPSErrors = true });
        try
        {
            await ApiAsync(context, "/api/auth/login", "POST",
                new { userName = BrowserFixture.AdminUserName, password = BrowserFixture.AdminPassword }, 204);
            await context.NewPageAsync();
            return context;
        }
        catch
        {
            await context.DisposeAsync();
            throw;
        }
    }

    private async Task<JsonElement> ApiAsync(IBrowserContext context, string path, string method = "GET",
        object? body = null, int expectedStatus = 200)
    {
        var headers = new Dictionary<string, string>();
        if (method != "GET")
        {
            var security = await context.APIRequest.GetAsync($"{fixture.BaseUrl}/api/auth/antiforgery");
            Assert.Equal(200, security.Status);
            using var tokens = JsonDocument.Parse(await security.TextAsync());
            headers["X-CSRF-TOKEN"] = tokens.RootElement.GetProperty("requestToken").GetString()!;
            await security.DisposeAsync();
        }
        var response = await context.APIRequest.FetchAsync($"{fixture.BaseUrl}{path}",
            new() { Method = method, DataObject = body, Headers = headers });
        var text = await response.TextAsync();
        Assert.True(response.Status == expectedStatus, $"{method} {path}: expected {expectedStatus}, got {response.Status}: {text}");
        await response.DisposeAsync();
        if (expectedStatus == 204) return default;
        using var document = JsonDocument.Parse(text);
        return document.RootElement.Clone();
    }

    private Task<JsonElement> CreateCourseAsync(IBrowserContext context, string description = "Existing description") =>
        ApiAsync(context, "/api/courses", "POST", new { code = $"P{Guid.NewGuid():N}", name = "Preservation course", description }, 201);

    private Task<JsonElement> CreateInstructorAsync(IBrowserContext context) =>
        ApiAsync(context, "/api/instructors", "POST", new { fullName = $"Preservation instructor {Guid.NewGuid():N}" }, 201);

    private Task<JsonElement> CreateGroupAsync(IBrowserContext context, string courseId, string? instructorId) =>
        ApiAsync(context, "/api/training-groups", "POST", new
        {
            name = $"Preservation group {Guid.NewGuid():N}", courseId, primaryInstructorId = instructorId,
            plannedStartDate = DateTime.UtcNow.AddDays(2).ToString("yyyy-MM-dd"),
            plannedEndDate = DateTime.UtcNow.AddDays(10).ToString("yyyy-MM-dd")
        }, 201);

    private Task<JsonElement> DeactivateAsync(IBrowserContext context, string kind, JsonElement record) =>
        ApiAsync(context, $"/api/{kind}/{Id(record)}/deactivate", "POST", new { rowVersion = Version(record) });

    private static string Id(JsonElement record) => record.GetProperty("id").GetString()!;
    private static string Version(JsonElement record) => record.GetProperty("rowVersion").GetString()!;

    private static async Task ExpectCurrentInactiveAsync(ILocator select, string id)
    {
        await Assertions.Expect(select).ToHaveValueAsync(id);
        var option = select.Locator($"option[value='{id}']");
        await Assertions.Expect(option).ToContainTextAsync("(inactive, current)");
        await Assertions.Expect(option).ToBeDisabledAsync();
    }

    private static Task ExpectMissingOptionAsync(ILocator select, string id) =>
        Assertions.Expect(select.Locator($"option[value='{id}']")).ToHaveCountAsync(0);

    private static async Task SaveAsync(IPage page, string button, string path)
    {
        var response = await page.RunAndWaitForResponseAsync(
            () => page.GetByRole(AriaRole.Button, new() { Name = button, Exact = true }).ClickAsync(),
            response => response.Request.Method == "PUT" && new Uri(response.Url).AbsolutePath == path);
        Assert.Equal(200, response.Status);
    }

    private static async Task ConfirmSaveAsync(IPage page, string button, string path)
    {
        await page.GetByRole(AriaRole.Button, new() { Name = button, Exact = true }).ClickAsync();
        var response = await page.RunAndWaitForResponseAsync(
            () => page.GetByRole(AriaRole.Button, new() { Name = "Confirm", Exact = true }).ClickAsync(),
            response => response.Request.Method is "PUT" or "PATCH" && new Uri(response.Url).AbsolutePath == path);
        Assert.Equal(200, response.Status);
    }
}
