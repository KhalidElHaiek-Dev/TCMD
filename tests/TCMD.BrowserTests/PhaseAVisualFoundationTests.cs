using System.Text.Json;
using Microsoft.Playwright;

namespace TCMD.BrowserTests;

[Collection(BrowserCollection.Name)]
public sealed class PhaseAVisualFoundationTests(BrowserFixture fixture)
{
    [Fact]
    public async Task AdministratorNavigation_RemainsAuthorized_AndTracksTheActiveRoute()
    {
        await using var context = await SignedInContextAsync(BrowserFixture.AdminUserName, BrowserFixture.AdminPassword);
        var page = context.Pages.Single();
        await page.GotoAsync($"{fixture.BaseUrl}/#/dashboard");

        var nav = page.GetByRole(AriaRole.Navigation, new() { Name = "Primary" });
        await Assertions.Expect(nav.GetByRole(AriaRole.Link)).ToHaveCountAsync(6);
        await Assertions.Expect(nav.GetByRole(AriaRole.Link, new() { Name = "Staff Accounts" })).ToBeVisibleAsync();
        await nav.GetByRole(AriaRole.Link, new() { Name = "Students", Exact = true }).ClickAsync();
        await Assertions.Expect(nav.GetByRole(AriaRole.Link, new() { Name = "Students", Exact = true })).ToHaveAttributeAsync("aria-current", "page");
        await Assertions.Expect(nav.GetByRole(AriaRole.Link, new() { Name = "Dashboard", Exact = true })).Not.ToHaveAttributeAsync("aria-current", "page");
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Sign out" })).ToBeVisibleAsync();
    }

    [Theory]
    [InlineData("Staff", 5, "Students")]
    [InlineData("Instructor", 2, "My Groups")]
    public async Task RoleNavigation_RemainsLimitedToAuthorizedLinks(string role, int linkCount, string expectedLink)
    {
        var userName = $"phase-a-{role.ToLowerInvariant()}-{Guid.NewGuid():N}";
        await using (var admin = await SignedInContextAsync(BrowserFixture.AdminUserName, BrowserFixture.AdminPassword))
            await ApiAsync(admin, "/api/staff-accounts", "POST", new { userName, displayName = $"Phase A {role}", password = "Password1", role }, 201);

        await using var context = await SignedInContextAsync(userName, "Password1");
        var page = context.Pages.Single();
        await page.GotoAsync($"{fixture.BaseUrl}/#/dashboard");
        var nav = page.GetByRole(AriaRole.Navigation, new() { Name = "Primary" });
        await Assertions.Expect(nav.GetByRole(AriaRole.Link)).ToHaveCountAsync(linkCount);
        await Assertions.Expect(nav.GetByRole(AriaRole.Link, new() { Name = expectedLink, Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(nav.GetByRole(AriaRole.Link, new() { Name = "Staff Accounts" })).ToHaveCountAsync(0);
        if (role == "Instructor")
            await Assertions.Expect(nav.GetByRole(AriaRole.Link, new() { Name = "Students" })).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task MobileDrawer_IsKeyboardOperable_ClosesWithEscape_AndRestoresFocus()
    {
        await using var context = await SignedInContextAsync(BrowserFixture.AdminUserName, BrowserFixture.AdminPassword, 375);
        var page = context.Pages.Single();
        await page.GotoAsync($"{fixture.BaseUrl}/#/dashboard");
        var trigger = page.Locator(".menu-button");

        await trigger.FocusAsync();
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(trigger).ToHaveAttributeAsync("aria-expanded", "true");
        await Assertions.Expect(page.Locator(".drawer-close")).ToBeFocusedAsync();
        await page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(trigger).ToHaveAttributeAsync("aria-expanded", "false");
        await Assertions.Expect(trigger).ToBeFocusedAsync();
    }

    [Theory]
    [InlineData(320)]
    [InlineData(375)]
    [InlineData(768)]
    public async Task ShellAndResponsiveTable_DoNotCreateCriticalHorizontalOverflow(int width)
    {
        await using var context = await SignedInContextAsync(BrowserFixture.AdminUserName, BrowserFixture.AdminPassword, width);
        await ApiAsync(context, "/api/students", "POST", new { fullName = $"Phase A responsive student {Guid.NewGuid():N}", phoneNumber = "+212600000008" }, 201);
        var page = context.Pages.Single();
        await page.GotoAsync($"{fixture.BaseUrl}/#/students");
        await Assertions.Expect(page.Locator(".responsive-table")).ToBeVisibleAsync();
        Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth + 1"));

        var trigger = page.GetByRole(AriaRole.Button, new() { Name = "Open navigation" });
        await trigger.ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Navigation, new() { Name = "Primary" })).ToBeVisibleAsync();
        Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth + 1"));
    }

    private async Task<IBrowserContext> SignedInContextAsync(string userName, string password, int width = 1280)
    {
        var context = await fixture.Browser.NewContextAsync(new()
        {
            IgnoreHTTPSErrors = true,
            ViewportSize = new() { Width = width, Height = 800 }
        });
        await ApiAsync(context, "/api/auth/login", "POST", new { userName, password }, 204);
        await context.NewPageAsync();
        return context;
    }

    private async Task<JsonElement> ApiAsync(IBrowserContext context, string path, string method, object body, int expectedStatus)
    {
        var security = await context.APIRequest.GetAsync($"{fixture.BaseUrl}/api/auth/antiforgery");
        using var tokens = JsonDocument.Parse(await security.TextAsync());
        var headers = new Dictionary<string, string> { ["X-CSRF-TOKEN"] = tokens.RootElement.GetProperty("requestToken").GetString()! };
        await security.DisposeAsync();
        var response = await context.APIRequest.FetchAsync($"{fixture.BaseUrl}{path}", new() { Method = method, DataObject = body, Headers = headers });
        var content = await response.TextAsync();
        Assert.True(response.Status == expectedStatus, $"{method} {path}: expected {expectedStatus}, got {response.Status}: {content}");
        await response.DisposeAsync();
        if (expectedStatus == 204) return default;
        using var document = JsonDocument.Parse(content);
        return document.RootElement.Clone();
    }
}
