using System.Text.Json;
using Microsoft.Playwright;

namespace TCMD.BrowserTests;

[Collection(BrowserCollection.Name)]
public sealed class PhaseBReferenceImplementationTests(BrowserFixture fixture)
{
    [Fact]
    public async Task AdministratorDashboard_ShowsMetricsActionsAndAttention_WithoutPerGroupReads()
    {
        await using var context = await SignInAsync(BrowserFixture.AdminUserName, BrowserFixture.AdminPassword);
        var page = context.Pages.Single();
        var detailReads = 0;
        await page.RouteAsync("**/api/training-groups/**", async route =>
        {
            if (new Uri(route.Request.Url).AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries).Length > 2)
                detailReads++;
            await route.ContinueAsync();
        });
        await page.RouteAsync("**/api/training-groups", route => route.FulfillAsync(new()
        {
            Status = 200, ContentType = "application/json",
            Body = "[{\"id\":\"11111111-1111-1111-1111-111111111111\",\"name\":\"Unassigned planned group\",\"courseId\":\"22222222-2222-2222-2222-222222222222\",\"primaryInstructorId\":null,\"plannedStartDate\":\"2026-10-01\",\"plannedEndDate\":\"2026-10-02\",\"status\":\"Planned\"}]"
        }));
        await page.GotoAsync($"{fixture.BaseUrl}/#/dashboard");

        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Dashboard", Exact = true })).ToBeVisibleAsync();
        foreach (var label in new[] { "Active Students", "Active Training Groups", "Active Courses", "Planned Training Groups" })
            await Assertions.Expect(page.GetByText(label, new() { Exact = true })).ToBeVisibleAsync();
        foreach (var action in new[] { "Add Student", "Create Training Group", "Add Course", "Add Instructor", "Staff Accounts" })
            await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = action, Exact = true }).First).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Groups requiring attention" })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("Unassigned planned group", new() { Exact = true })).ToBeVisibleAsync();
        Assert.Equal(0, detailReads);
    }

    [Fact]
    public async Task StaffDashboard_HidesAdministratorOnlyAction()
    {
        var userName = $"phase-b-staff-{Guid.NewGuid():N}";
        await using (var admin = await SignInAsync(BrowserFixture.AdminUserName, BrowserFixture.AdminPassword))
            await ApiAsync(admin, "/api/staff-accounts", "POST", new { userName, displayName = "Phase B Staff", password = "Password1", role = "Staff" }, 201);
        await using var context = await SignInAsync(userName, "Password1");
        var page = context.Pages.Single();
        await page.GotoAsync($"{fixture.BaseUrl}/#/dashboard");
        await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "Add Student", Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "Staff Accounts", Exact = true })).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task AdministratorDashboard_ShowsAttentionEmptyState_WhenNoPlannedGroupNeedsAnInstructor()
    {
        await using var context = await SignInAsync(BrowserFixture.AdminUserName, BrowserFixture.AdminPassword);
        var page = context.Pages.Single();
        await page.RouteAsync("**/api/training-groups", route => route.FulfillAsync(new()
        {
            Status = 200, ContentType = "application/json", Body = "[]"
        }));
        await page.GotoAsync($"{fixture.BaseUrl}/#/dashboard");
        await Assertions.Expect(page.GetByText("No groups currently require an Instructor.", new() { Exact = true })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task LinkedInstructorDashboard_ShowsOnlyAssignedWorkflow()
    {
        var userName = $"phase-b-instructor-{Guid.NewGuid():N}";
        await using var admin = await SignInAsync(BrowserFixture.AdminUserName, BrowserFixture.AdminPassword);
        var instructor = await ApiAsync(admin, "/api/instructors", "POST", new { fullName = "Phase B Instructor" }, 201);
        var course = await ApiAsync(admin, "/api/courses", "POST", new { code = $"PB{Guid.NewGuid():N}"[..18], name = "Phase B Course", description = "" }, 201);
        var group = await ApiAsync(admin, "/api/training-groups", "POST", new { name = "Phase B assigned group", courseId = Id(course), primaryInstructorId = Id(instructor), plannedStartDate = "2026-09-26", plannedEndDate = "2026-09-27" }, 201);
        var account = await ApiAsync(admin, "/api/staff-accounts", "POST", new { userName, displayName = "Phase B Instructor Account", password = "Password1", role = "Instructor" }, 201);
        await ApiAsync(admin, $"/api/staff-accounts/{Id(account)}/instructor-link", "PUT", new { instructorId = Id(instructor) });

        await using var context = await SignInAsync(userName, "Password1");
        var page = context.Pages.Single();
        await page.GotoAsync($"{fixture.BaseUrl}/#/dashboard");
        await Assertions.Expect(page.GetByText("Linked and active", new() { Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("Phase B assigned group", new() { Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "View Phase B assigned group" })).ToHaveAttributeAsync("href", $"#/groups/{Id(group)}");
        await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "Add Student" })).ToHaveCountAsync(0);
        await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "Staff Accounts" })).ToHaveCountAsync(0);
    }

    [Theory]
    [InlineData(320)]
    [InlineData(375)]
    [InlineData(768)]
    public async Task DashboardAndStudentReferenceScreens_DoNotOverflow(int width)
    {
        await using var context = await SignInAsync(BrowserFixture.AdminUserName, BrowserFixture.AdminPassword, width);
        var student = await ApiAsync(context, "/api/students", "POST", new { fullName = $"Phase B Student {Guid.NewGuid():N}", phoneNumber = "+212600000009", email = "phaseb@example.test" }, 201);
        var page = context.Pages.Single();
        foreach (var route in new[] { "dashboard", "students", $"students/{Id(student)}", "students/new" })
        {
            await page.GotoAsync($"{fixture.BaseUrl}/#/{route}");
            Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth + 1"), $"Overflow at {width}px on {route}");
        }
    }

    [Fact]
    public async Task StudentPages_ExposeCanonicalHeaderFiltersOverviewAndActions()
    {
        await using var context = await SignInAsync(BrowserFixture.AdminUserName, BrowserFixture.AdminPassword);
        var student = await ApiAsync(context, "/api/students", "POST", new { fullName = "Phase B canonical student", phoneNumber = "+212600000010", email = "canonical@example.test" }, 201);
        var page = context.Pages.Single();
        await page.GotoAsync($"{fixture.BaseUrl}/#/students");
        await Assertions.Expect(page.Locator(".page-header").GetByRole(AriaRole.Link, new() { Name = "Add Student" })).ToBeVisibleAsync();
        await page.GetByLabel("Search").FillAsync("Phase B canonical");
        await page.GetByLabel("Status").SelectOptionAsync("true");
        await page.GetByRole(AriaRole.Button, new() { Name = "Search", Exact = true }).ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Cell, new() { Name = "Active", Exact = true })).ToBeVisibleAsync();
        await page.GetByRole(AriaRole.Link, new() { Name = $"View {student.GetProperty("studentNumber").GetString()}" }).ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Phase B canonical student", Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Overview", Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Edit details", Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Deactivate", Exact = true })).ToBeVisibleAsync();
        await page.GetByRole(AriaRole.Link, new() { Name = "Back to Students", Exact = false }).ClickAsync();
        await Assertions.Expect(page.GetByLabel("Search")).ToHaveValueAsync("Phase B canonical");
    }

    private async Task<IBrowserContext> SignInAsync(string userName, string password, int width = 1280)
    {
        var context = await fixture.Browser.NewContextAsync(new() { IgnoreHTTPSErrors = true, ViewportSize = new() { Width = width, Height = 900 } });
        await ApiAsync(context, "/api/auth/login", "POST", new { userName, password }, 204);
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
