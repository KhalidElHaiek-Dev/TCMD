using System.Text.Json;
using Microsoft.Playwright;

namespace TCMD.BrowserTests;

[Collection(BrowserCollection.Name)]
public sealed class PhaseCSystemVisualApplicationTests(BrowserFixture fixture)
{
    [Theory]
    [InlineData("instructors", "Instructors", "Add Instructor")]
    [InlineData("courses", "Courses", "Add Course")]
    public async Task EntityModules_UseCanonicalListAndDetailHierarchy(string route, string title, string action)
    {
        await using var context = await SignInAsync();
        var item = route == "instructors"
            ? await ApiAsync(context, "/api/instructors", "POST", new { fullName = "Phase C Instructor", phoneNumber = "+212600000020", email = "phasec-instructor@example.test" }, 201)
            : await ApiAsync(context, "/api/courses", "POST", new { code = $"PC{Guid.NewGuid():N}"[..18], name = "Phase C Course", description = "Presentation-ready course" }, 201);
        var page = context.Pages.Single();

        await page.GotoAsync($"{fixture.BaseUrl}/#/{route}");
        await Assertions.Expect(page.Locator(".page-header").GetByRole(AriaRole.Heading, new() { Name = title, Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator(".page-header").GetByRole(AriaRole.Link, new() { Name = action, Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Columnheader, new() { Name = "Actions", Exact = true })).ToBeVisibleAsync();

        await page.GotoAsync($"{fixture.BaseUrl}/#/{route}/{Id(item)}");
        await Assertions.Expect(page.Locator(".entity-header")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Overview", Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Edit details", Exact = true })).ToHaveAttributeAsync("aria-expanded", "false");
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Edit details", Exact = true })).ToBeHiddenAsync();
        await Assertions.Expect(page.Locator(".danger-zone").GetByRole(AriaRole.Button, new() { Name = "Deactivate", Exact = true })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task GroupAndSession_ExposeEntityOverviewTabsAndSeparatedLifecycleActions()
    {
        await using var context = await SignInAsync();
        var course = await ApiAsync(context, "/api/courses", "POST", new { code = $"PC{Guid.NewGuid():N}"[..18], name = "Phase C Group Course", description = "" }, 201);
        var instructor = await ApiAsync(context, "/api/instructors", "POST", new { fullName = "Phase C Group Instructor" }, 201);
        var group = await ApiAsync(context, "/api/training-groups", "POST", new { name = "Phase C Training Group", courseId = Id(course), primaryInstructorId = Id(instructor), plannedStartDate = "2026-09-27", plannedEndDate = "2026-09-29" }, 201);
        var session = await ApiAsync(context, $"/api/training-groups/{Id(group)}/sessions", "POST", new { sessionDate = "2026-09-27", startTime = "09:00", endTime = "10:00", location = "Room C" }, 201);
        var page = context.Pages.Single();

        await page.GotoAsync($"{fixture.BaseUrl}/#/groups/{Id(group)}");
        await Assertions.Expect(page.Locator(".entity-header").GetByRole(AriaRole.Heading, new() { Name = "Phase C Training Group", Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator(".overview").GetByText("Phase C Group Course (", new() { Exact = false })).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator(".overview").GetByText("Phase C Group Instructor", new() { Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Navigation, new() { Name = "Training Group sections" })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Edit details", Exact = true })).ToHaveAttributeAsync("aria-expanded", "false");
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Edit details", Exact = true })).ToBeHiddenAsync();
        await Assertions.Expect(page.Locator(".lifecycle-panel").GetByRole(AriaRole.Button, new() { Name = "Activate", Exact = true })).ToBeVisibleAsync();

        await page.GotoAsync($"{fixture.BaseUrl}/#/sessions/{Id(session)}");
        await Assertions.Expect(page.Locator(".entity-header")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Overview", Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Edit details", Exact = true })).ToHaveAttributeAsync("aria-expanded", "false");
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Edit details", Exact = true })).ToBeHiddenAsync();
        await Assertions.Expect(page.Locator(".lifecycle-panel").GetByRole(AriaRole.Button, new() { Name = "Complete", Exact = true })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task StaffAccount_UsesIdentityHeaderAndSeparatedOperationalSections()
    {
        await using var context = await SignInAsync();
        var account = await ApiAsync(context, "/api/staff-accounts", "POST", new { userName = $"phase-c-{Guid.NewGuid():N}", displayName = "Phase C Staff", password = "Password1", role = "Staff" }, 201);
        var page = context.Pages.Single();
        await page.GotoAsync($"{fixture.BaseUrl}/#/staff-accounts/{Id(account)}");

        await Assertions.Expect(page.Locator(".entity-header").GetByRole(AriaRole.Heading, new() { Name = "Phase C Staff", Exact = true })).ToBeVisibleAsync();
        foreach (var heading in new[] { "Overview", "Role & access", "Password replacement", "Activation / deactivation" })
            await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = heading, Exact = true })).ToBeVisibleAsync();
    }

    [Theory]
    [InlineData(320)]
    [InlineData(375)]
    [InlineData(768)]
    public async Task RepresentativePhaseCScreens_DoNotOverflow(int width)
    {
        await using var context = await SignInAsync(width);
        var page = context.Pages.Single();
        foreach (var route in new[] { "instructors", "instructors/new", "courses", "courses/new", "groups", "groups/new", "staff-accounts", "staff-accounts/new" })
        {
            await page.GotoAsync($"{fixture.BaseUrl}/#/{route}");
            Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth + 1"), $"Overflow at {width}px on {route}");
        }
    }

    private async Task<IBrowserContext> SignInAsync(int width = 1280)
    {
        var context = await fixture.Browser.NewContextAsync(new() { IgnoreHTTPSErrors = true, ViewportSize = new() { Width = width, Height = 900 } });
        await ApiAsync(context, "/api/auth/login", "POST", new { userName = BrowserFixture.AdminUserName, password = BrowserFixture.AdminPassword }, 204);
        await context.NewPageAsync();
        return context;
    }

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

    private static string Id(JsonElement item) => item.GetProperty("id").GetString()!;
}
