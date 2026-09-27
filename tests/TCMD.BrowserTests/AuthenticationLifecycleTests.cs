using System.Text.Json;
using Microsoft.Playwright;

namespace TCMD.BrowserTests;

[Collection(BrowserCollection.Name)]
public sealed class AuthenticationLifecycleTests(BrowserFixture fixture)
{
    [Fact]
    public async Task DelayedPageRequest_FollowedByNavigation_CannotReplaceFinalRoute()
    {
        await using var context = await SignInAsync();
        var page = await context.NewPageAsync();
        var requested = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await page.GotoAsync($"{fixture.BaseUrl}/#/dashboard");
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Dashboard" })).ToBeVisibleAsync();
        await page.RouteAsync("**/api/students?*", async route => { requested.SetResult(); await release.Task; await route.ContinueAsync(); });
        await page.EvaluateAsync("location.hash='#/students'");
        await requested.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await NavigateAndWaitAsync(page, "/courses", "/api/courses");
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Courses", Exact = true })).ToBeVisibleAsync();
        release.SetResult();
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Students", Exact = true })).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task DelayedAuthenticatedRequest_FollowedByLogout_CannotRestoreProtectedContent()
    {
        await using var context = await SignInAsync();
        var page = await OpenDashboardAsync(context);
        var requested = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await page.RouteAsync("**/api/students?*", async route => { requested.SetResult(); await release.Task; await route.ContinueAsync(); });
        await page.EvaluateAsync("location.hash='#/students'");
        await requested.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await page.GetByRole(AriaRole.Button, new() { Name = "Sign out" }).ClickAsync();
        release.SetResult();
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Sign in to TCMD" })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Students", Exact = true })).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task LogoutThenRelogin_OldSessionRequestCannotRenderIntoNewSession()
    {
        await using var context = await SignInAsync();
        var page = await OpenDashboardAsync(context);
        var requested = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await page.RouteAsync("**/api/students?*", async route => { requested.SetResult(); await release.Task; await route.ContinueAsync(); });
        await page.EvaluateAsync("location.hash='#/students'");
        await requested.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await page.GetByRole(AriaRole.Button, new() { Name = "Sign out" }).ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Sign in to TCMD" })).ToBeVisibleAsync();
        await page.EvaluateAsync("location.hash='#/courses'");
        await SignInFormAsync(page);
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Courses", Exact = true })).ToBeVisibleAsync();
        release.SetResult();
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Students", Exact = true })).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task MalformedBody401_InvalidatesSession_AndAllowsFreshLogin()
    {
        await using var context = await SignInAsync();
        var page = await OpenDashboardAsync(context);
        await page.RouteAsync("**/api/students?*", route => route.FulfillAsync(new() { Status = 401, ContentType = "application/json", Body = "{" }));
        await page.EvaluateAsync("location.hash='#/students'");
        await Assertions.Expect(page.GetByText("Your session expired. Sign in again.")).ToBeVisibleAsync();
        await page.UnrouteAsync("**/api/students?*");
        await SignInFormAsync(page);
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Students", Exact = true })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task LoginAntiforgeryFailure_ShowsFeedback_AndRestoresButton()
    {
        await using var context = await fixture.Browser.NewContextAsync(new() { IgnoreHTTPSErrors = true });
        var page = await context.NewPageAsync();
        var calls = 0;
        await page.RouteAsync("**/api/auth/antiforgery", async route =>
        {
            if (++calls == 1) await route.ContinueAsync();
            else await route.FulfillAsync(new() { Status = 503, ContentType = "application/json", Body = "{}" });
        });
        await page.GotoAsync(fixture.BaseUrl);
        await SignInFormAsync(page, expectSuccess: false);
        await Assertions.Expect(page.GetByRole(AriaRole.Alert)).ToContainTextAsync("Request security could not be initialized.");
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Sign in" })).ToBeEnabledAsync();
    }

    [Fact]
    public async Task LogoutAntiforgeryFailure_IsVisible_AndKeepsAuthenticatedState()
    {
        await using var context = await SignInAsync();
        var page = await OpenDashboardAsync(context);
        await page.RouteAsync("**/api/auth/antiforgery", route => route.FulfillAsync(new() { Status = 503, ContentType = "application/json", Body = "{}" }));
        await page.GetByRole(AriaRole.Button, new() { Name = "Sign out" }).ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Alert)).ToContainTextAsync("Request security could not be initialized.");
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Sign out" })).ToBeEnabledAsync();
        await Assertions.Expect(page.Locator(".identity-name")).ToHaveTextAsync("Browser Administrator");
        await Assertions.Expect(page.Locator(".identity-role")).ToHaveTextAsync("Administrator");
    }

    [Fact]
    public async Task StartupNetworkFailure_IsNotPresentedAsSignedOut_AndCanRetry()
    {
        await using var context = await fixture.Browser.NewContextAsync(new() { IgnoreHTTPSErrors = true });
        var page = await context.NewPageAsync();
        await page.RouteAsync("**/api/auth/session", route => route.AbortAsync());
        await page.GotoAsync(fixture.BaseUrl);
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Sign in to TCMD" })).ToHaveCountAsync(0);
        await Assertions.Expect(page.GetByText("TCMD could not confirm your session.")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Try again" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task RapidRouteChanges_RenderOnlyTheFinalRoute()
    {
        await using var context = await SignInAsync();
        var page = await OpenDashboardAsync(context);
        var requested = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await page.RouteAsync("**/api/students?*", async route => { requested.SetResult(); await release.Task; await route.ContinueAsync(); });
        await page.EvaluateAsync("location.hash='#/students'");
        await requested.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await page.EvaluateAsync("location.hash='#/instructors'");
        await NavigateAndWaitAsync(page, "/courses", "/api/courses");
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Courses", Exact = true })).ToBeVisibleAsync();
        release.SetResult();
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Students", Exact = true })).ToHaveCountAsync(0);
    }

    private async Task<IBrowserContext> SignInAsync()
    {
        var context = await fixture.Browser.NewContextAsync(new() { IgnoreHTTPSErrors = true });
        var security = await context.APIRequest.GetAsync($"{fixture.BaseUrl}/api/auth/antiforgery");
        using var tokens = JsonDocument.Parse(await security.TextAsync());
        var response = await context.APIRequest.PostAsync($"{fixture.BaseUrl}/api/auth/login", new()
        {
            DataObject = new { userName = BrowserFixture.AdminUserName, password = BrowserFixture.AdminPassword },
            Headers = new Dictionary<string, string> { ["X-CSRF-TOKEN"] = tokens.RootElement.GetProperty("requestToken").GetString()! }
        });
        Assert.Equal(204, response.Status);
        return context;
    }

    private async Task<IPage> OpenDashboardAsync(IBrowserContext context)
    {
        var page = await context.NewPageAsync();
        await page.GotoAsync($"{fixture.BaseUrl}/#/dashboard");
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Dashboard" })).ToBeVisibleAsync();
        return page;
    }

    private static async Task SignInFormAsync(IPage page, bool expectSuccess = true)
    {
        await page.GetByLabel("Username").FillAsync(BrowserFixture.AdminUserName);
        await page.GetByLabel("Password").FillAsync(BrowserFixture.AdminPassword);
        await page.GetByRole(AriaRole.Button, new() { Name = "Sign in" }).ClickAsync();
        if (expectSuccess)
        {
            await Assertions.Expect(page.Locator(".identity-name")).ToHaveTextAsync("Browser Administrator");
            await Assertions.Expect(page.Locator(".identity-role")).ToHaveTextAsync("Administrator");
        }
    }

    private static async Task NavigateAndWaitAsync(IPage page, string route, string apiPath)
    {
        await page.RunAndWaitForResponseAsync(
            () => page.EvaluateAsync($"location.hash='#{route}'"),
            response => response.Request.Method == "GET" && new Uri(response.Url).AbsolutePath == apiPath);
    }
}
