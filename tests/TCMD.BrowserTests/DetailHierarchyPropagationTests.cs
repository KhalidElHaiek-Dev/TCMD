using System.Text.Json;
using Microsoft.Playwright;

namespace TCMD.BrowserTests;

[Collection(BrowserCollection.Name)]
public sealed class DetailHierarchyPropagationTests(BrowserFixture fixture)
{
    [Fact]
    public async Task Instructor_PrioritizesOverviewAndAssignments_AndEditorCanSaveAndCancel()
    {
        await using var context = await SignInAsync();
        var instructor = await ApiAsync(context, "/api/instructors", "POST", new
        {
            fullName = "Hierarchy instructor", phoneNumber = "+212600000040", email = "instructor@example.test"
        }, 201);
        var page = context.Pages.Single();
        await page.GotoAsync($"{fixture.BaseUrl}/#/instructors/{Id(instructor)}");

        var edit = page.GetByRole(AriaRole.Button, new() { Name = "Edit details", Exact = true });
        await Assertions.Expect(edit).ToHaveAttributeAsync("aria-expanded", "false");
        await Assertions.Expect(page.Locator("#instructors-edit-region")).ToBeHiddenAsync();
        Assert.True(await IsBeforeAsync(page, ".overview", ".edit-disclosure"));
        Assert.True(await IsBeforeAsync(page, "section:has(h2:text-is('Assigned groups'))", ".edit-disclosure"));
        Assert.True(await IsBeforeAsync(page, ".edit-disclosure", ".danger-zone"));

        await edit.ClickAsync();
        await Assertions.Expect(page.GetByLabel("Full name")).ToBeFocusedAsync();
        await page.GetByLabel("Full name").FillAsync("Unsaved instructor");
        await page.GetByRole(AriaRole.Button, new() { Name = "Cancel editing", Exact = true }).ClickAsync();
        await Assertions.Expect(edit).ToBeFocusedAsync();
        await edit.ClickAsync();
        await Assertions.Expect(page.GetByLabel("Full name")).ToHaveValueAsync("Hierarchy instructor");
        await page.GetByLabel("Full name").FillAsync("Saved hierarchy instructor");
        await SaveAsync(page, $"/api/instructors/{Id(instructor)}", "Save changes");
        await Assertions.Expect(page.Locator(".entity-header").GetByRole(AriaRole.Heading, new() { Name = "Saved hierarchy instructor", Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator(".overview")).ToContainTextAsync("Saved hierarchy instructor");
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Deactivate", Exact = true })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Course_PrioritizesDescriptionAndAssociations_AndPreservesDescriptionOnSave()
    {
        await using var context = await SignInAsync();
        var course = await ApiAsync(context, "/api/courses", "POST", new
        {
            code = $"DH{Guid.NewGuid():N}"[..20], name = "Hierarchy course", description = "Preserve this course description"
        }, 201);
        var page = context.Pages.Single();
        await page.GotoAsync($"{fixture.BaseUrl}/#/courses/{Id(course)}");

        var edit = page.GetByRole(AriaRole.Button, new() { Name = "Edit details", Exact = true });
        await Assertions.Expect(page.Locator(".overview")).ToContainTextAsync("Preserve this course description");
        await Assertions.Expect(edit).ToHaveAttributeAsync("aria-expanded", "false");
        await Assertions.Expect(page.Locator("#courses-edit-region")).ToBeHiddenAsync();
        Assert.True(await IsBeforeAsync(page, ".overview", ".edit-disclosure"));
        Assert.True(await IsBeforeAsync(page, "section:has(h2:text-is('Associated groups'))", ".edit-disclosure"));
        Assert.True(await IsBeforeAsync(page, ".edit-disclosure", ".danger-zone"));

        await edit.ClickAsync();
        await page.GetByLabel("Name", new() { Exact = true }).FillAsync("Saved hierarchy course");
        await SaveAsync(page, $"/api/courses/{Id(course)}", "Save changes");
        var stored = await ApiAsync(context, $"/api/courses/{Id(course)}");
        Assert.Equal("Preserve this course description", stored.GetProperty("description").GetString());
        await Assertions.Expect(page.Locator(".overview")).ToContainTextAsync("Preserve this course description");
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Deactivate", Exact = true })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Session_PrioritizesAttendance_AndEditorUpdatesAuthoritativePresentation()
    {
        await using var context = await SignInAsync();
        var session = await CreateSessionAsync(context);
        var page = context.Pages.Single();
        await page.GotoAsync($"{fixture.BaseUrl}/#/sessions/{Id(session)}");

        var edit = page.GetByRole(AriaRole.Button, new() { Name = "Edit details", Exact = true });
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Attendance roster", Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(edit).ToHaveAttributeAsync("aria-expanded", "false");
        await Assertions.Expect(page.Locator("#session-edit-region")).ToBeHiddenAsync();
        Assert.True(await IsBeforeAsync(page, "h2:text-is('Attendance roster')", ".edit-disclosure"));
        Assert.True(await IsBeforeAsync(page, ".edit-disclosure", ".lifecycle-panel"));
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Complete", Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Cancel", Exact = true })).ToBeVisibleAsync();

        await edit.ClickAsync();
        await Assertions.Expect(page.GetByLabel("Session date")).ToBeFocusedAsync();
        await page.GetByLabel("Location").FillAsync("Operational room");
        await SaveAsync(page, $"/api/training-sessions/{Id(session)}", "Save Session");
        await Assertions.Expect(page.Locator(".overview")).ToContainTextAsync("Operational room");
        await page.GetByLabel("Location").FillAsync("Unsaved room");
        await page.GetByRole(AriaRole.Button, new() { Name = "Cancel editing", Exact = true }).ClickAsync();
        await Assertions.Expect(edit).ToBeFocusedAsync();
        await edit.ClickAsync();
        await Assertions.Expect(page.GetByLabel("Location")).ToHaveValueAsync("Operational room");
    }

    [Theory]
    [InlineData(320)]
    [InlineData(375)]
    [InlineData(768)]
    public async Task PropagatedDetailHierarchy_RemainsReachableWithoutOverflow(int width)
    {
        await using var context = await SignInAsync(width);
        var instructor = await ApiAsync(context, "/api/instructors", "POST", new { fullName = $"Responsive instructor {width}" }, 201);
        var course = await ApiAsync(context, "/api/courses", "POST", new { code = $"R{width}{Guid.NewGuid():N}"[..20], name = $"Responsive course {width}", description = "Responsive description" }, 201);
        var session = await CreateSessionAsync(context, Id(course), Id(instructor));
        var page = context.Pages.Single();
        foreach (var route in new[] { $"/instructors/{Id(instructor)}", $"/courses/{Id(course)}", $"/sessions/{Id(session)}" })
        {
            await page.GotoAsync($"{fixture.BaseUrl}/#{route}");
            await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Edit details", Exact = true })).ToBeVisibleAsync();
            Assert.False(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth > document.documentElement.clientWidth"));
        }
    }

    private static Task<bool> IsBeforeAsync(IPage page, string first, string second) =>
        page.Locator(first).EvaluateAsync<bool>("(first, second) => Boolean(first.compareDocumentPosition(document.querySelector(second)) & Node.DOCUMENT_POSITION_FOLLOWING)", second);

    private static async Task SaveAsync(IPage page, string path, string button)
    {
        var response = await page.RunAndWaitForResponseAsync(
            () => page.GetByRole(AriaRole.Button, new() { Name = button, Exact = true }).ClickAsync(),
            response => response.Request.Method == "PUT" && new Uri(response.Url).AbsolutePath == path);
        Assert.Equal(200, response.Status);
        await Assertions.Expect(page.GetByRole(AriaRole.Status).Filter(new() { HasText = "Changes saved." })).ToBeVisibleAsync();
    }

    private async Task<IBrowserContext> SignInAsync(int width = 1280)
    {
        var context = await fixture.Browser.NewContextAsync(new() { IgnoreHTTPSErrors = true, ViewportSize = new() { Width = width, Height = 900 } });
        await ApiAsync(context, "/api/auth/login", "POST", new { userName = BrowserFixture.AdminUserName, password = BrowserFixture.AdminPassword }, 204);
        await context.NewPageAsync();
        return context;
    }

    private async Task<JsonElement> CreateSessionAsync(IBrowserContext context, string? courseId = null, string? instructorId = null)
    {
        if (courseId is null)
        {
            var course = await ApiAsync(context, "/api/courses", "POST", new { code = $"S{Guid.NewGuid():N}"[..20], name = "Session hierarchy course", description = "" }, 201);
            courseId = Id(course);
        }
        var group = await ApiAsync(context, "/api/training-groups", "POST", new
        {
            name = "Session hierarchy group", courseId, primaryInstructorId = instructorId,
            plannedStartDate = DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-dd"), plannedEndDate = DateTime.UtcNow.AddDays(3).ToString("yyyy-MM-dd")
        }, 201);
        return await ApiAsync(context, $"/api/training-groups/{Id(group)}/sessions", "POST", new
        {
            sessionDate = group.GetProperty("plannedStartDate").GetString(), startTime = "09:00", endTime = "10:00", location = "Initial room"
        }, 201);
    }

    private async Task<JsonElement> ApiAsync(IBrowserContext context, string path, string method = "GET", object? body = null, int status = 200)
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
        Assert.True(response.Status == status, $"{method} {path}: expected {status}, got {response.Status}: {content}");
        await response.DisposeAsync();
        if (status == 204) return default;
        using var document = JsonDocument.Parse(content);
        return document.RootElement.Clone();
    }

    private static string Id(JsonElement record) => record.GetProperty("id").GetString()!;
}
