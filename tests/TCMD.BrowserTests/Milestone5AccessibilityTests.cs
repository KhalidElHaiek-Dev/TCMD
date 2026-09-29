using System.Text.Json;
using Microsoft.Playwright;

namespace TCMD.BrowserTests;

[Collection(BrowserCollection.Name)]
public sealed class Milestone5AccessibilityTests(BrowserFixture fixture)
{
    [Fact]
    public async Task MouseRouteNavigation_FocusesMain_WithoutVisibleFocusTreatment()
    {
        await using var context = await SignInAsync();
        var page = context.Pages.Single();
        await page.GotoAsync($"{fixture.BaseUrl}/#/dashboard");

        await page.GetByRole(AriaRole.Navigation, new() { Name = "Primary" })
            .GetByRole(AriaRole.Link, new() { Name = "Students", Exact = true })
            .ClickAsync();

        var main = page.GetByRole(AriaRole.Main);
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Students" })).ToBeVisibleAsync();
        await Assertions.Expect(main).ToBeFocusedAsync();
        Assert.Equal("none", await main.EvaluateAsync<string>("element => getComputedStyle(element).outlineStyle"));
    }

    [Fact]
    public async Task SkipLink_UsesKeyboard_FocusesMain_AndPreservesRoute()
    {
        await using var context = await SignInAsync();
        var page = context.Pages.Single();
        await page.GotoAsync($"{fixture.BaseUrl}/#/students");
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Students" })).ToBeVisibleAsync();
        var originalUrl = page.Url;

        var skip = page.GetByRole(AriaRole.Link, new() { Name = "Skip to main content" });
        await skip.FocusAsync();
        await page.Keyboard.PressAsync("Enter");

        await Assertions.Expect(page.GetByRole(AriaRole.Main)).ToBeFocusedAsync();
        Assert.NotEqual("none", await page.GetByRole(AriaRole.Main)
            .EvaluateAsync<string>("element => getComputedStyle(element).outlineStyle"));
        Assert.Equal(originalUrl, page.Url);
    }

    [Fact]
    public async Task KeyboardTab_ToInteractiveControl_ShowsVisibleFocusTreatment()
    {
        await using var context = await SignInAsync();
        var page = context.Pages.Single();
        await page.GotoAsync($"{fixture.BaseUrl}/#/students");
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Students" })).ToBeVisibleAsync();

        await page.Keyboard.PressAsync("Tab");

        var addStudent = page.GetByRole(AriaRole.Link, new() { Name = "Add Student" });
        await Assertions.Expect(addStudent).ToBeFocusedAsync();
        Assert.NotEqual("none", await addStudent
            .EvaluateAsync<string>("element => getComputedStyle(element).outlineStyle"));
    }

    [Fact]
    public async Task Dialog_EscapeSettles_RemovesDialog_RestoresFocus_AndCanReopen()
    {
        await using var context = await SignInAsync();
        var student = await ApiAsync(context, "/api/students", "POST", new
        {
            fullName = $"Dialog Student {Guid.NewGuid():N}", phoneNumber = "+212600000005"
        }, 201);
        var page = context.Pages.Single();
        await page.GotoAsync($"{fixture.BaseUrl}/#/students/{student.GetProperty("id").GetString()}");
        var opener = page.GetByRole(AriaRole.Button, new() { Name = "Deactivate", Exact = true });

        await opener.ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Dialog)).ToBeVisibleAsync();
        await page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(page.GetByRole(AriaRole.Dialog)).ToHaveCountAsync(0);
        await Assertions.Expect(opener).ToBeFocusedAsync();
        await Assertions.Expect(page.Locator(".entity-header").GetByText("Active", new() { Exact = true })).ToBeVisibleAsync();

        await opener.ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Dialog)).ToBeVisibleAsync();
        await page.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Button, new() { Name = "Cancel" }).ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Dialog)).ToHaveCountAsync(0);
        await Assertions.Expect(opener).ToBeFocusedAsync();
    }

    [Fact]
    public async Task Validation_AssociatesHelpAndError_AndAnnouncesFailure()
    {
        await using var context = await SignInAsync();
        var page = context.Pages.Single();
        await page.GotoAsync($"{fixture.BaseUrl}/#/staff-accounts/new");
        await page.GetByLabel("Username").FillAsync($"a11y-{Guid.NewGuid():N}");
        await page.GetByLabel("Display name").FillAsync("Accessibility account");
        var password = page.GetByLabel("Initial password");
        await password.FillAsync("short");
        await page.GetByLabel("Role").SelectOptionAsync("Staff");
        await page.GetByRole(AriaRole.Button, new() { Name = "Create Account" }).ClickAsync();

        await Assertions.Expect(password).ToHaveAttributeAsync("aria-invalid", "true");
        var ids = (await password.GetAttributeAsync("aria-describedby"))!.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, ids.Length);
        await Assertions.Expect(page.Locator($"#{ids[0]}")).ToContainTextAsync("At least 8 characters");
        await Assertions.Expect(page.Locator($"#{ids[1]}")).ToContainTextAsync("Password does not meet");
        await Assertions.Expect(page.Locator("#announcer")).ToContainTextAsync("Password does not meet");

        await password.FillAsync("Password1");
        await page.GetByRole(AriaRole.Button, new() { Name = "Create Account" }).ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Staff Account", Exact = true })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task KeyboardOnly_CanCompleteRepresentativeCreateWorkflow()
    {
        await using var context = await SignInAsync();
        var page = context.Pages.Single();
        await page.GotoAsync($"{fixture.BaseUrl}/#/students/new");
        await Assertions.Expect(page.GetByRole(AriaRole.Main)).ToBeFocusedAsync();

        var studentName = $"Keyboard Student {Guid.NewGuid():N}";
        await page.Keyboard.PressAsync("Tab");
        await page.Keyboard.TypeAsync(studentName);
        await page.Keyboard.PressAsync("Tab");
        await page.Keyboard.TypeAsync("+212600000007");
        await page.Keyboard.PressAsync("Tab");
        await page.Keyboard.PressAsync("Tab");
        await page.Keyboard.PressAsync("Enter");

        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = studentName, Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator(".entity-header").GetByText("Active", new() { Exact = true })).ToBeVisibleAsync();
    }

    [Theory]
    [InlineData(320)]
    [InlineData(375)]
    [InlineData(768)]
    public async Task OperationalPageAndDialog_RemainUsableAtRepresentativeWidths(int width)
    {
        await using var context = await SignInAsync(width);
        var student = await ApiAsync(context, "/api/students", "POST", new
        {
            fullName = $"Responsive Student With A Long Name {Guid.NewGuid():N}", phoneNumber = "+212600000006"
        }, 201);
        var page = context.Pages.Single();
        await page.GotoAsync($"{fixture.BaseUrl}/#/students/{student.GetProperty("id").GetString()}");
        var opener = page.GetByRole(AriaRole.Button, new() { Name = "Deactivate", Exact = true });
        await Assertions.Expect(opener).ToBeInViewportAsync();
        await opener.ClickAsync();
        var dialog = page.GetByRole(AriaRole.Dialog);
        await Assertions.Expect(dialog).ToBeInViewportAsync();
        await Assertions.Expect(dialog.GetByRole(AriaRole.Button, new() { Name = "Cancel" })).ToBeInViewportAsync();
        await page.Keyboard.PressAsync("Escape");
    }

    private async Task<IBrowserContext> SignInAsync(int width = 1280)
    {
        var context = await fixture.Browser.NewContextAsync(new()
        {
            IgnoreHTTPSErrors = true,
            ViewportSize = new() { Width = width, Height = 800 }
        });
        await ApiAsync(context, "/api/auth/login", "POST",
            new { userName = BrowserFixture.AdminUserName, password = BrowserFixture.AdminPassword }, 204);
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
}
