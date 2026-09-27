using System.Text.Json;
using Microsoft.Playwright;

namespace TCMD.BrowserTests;

[Collection(BrowserCollection.Name)]
public sealed class PhaseDFinalPolishTests(BrowserFixture fixture)
{
    [Theory]
    [InlineData(320)]
    [InlineData(375)]
    [InlineData(768)]
    [InlineData(1024)]
    public async Task Login_IsKeyboardReadyAndDoesNotOverflow(int width)
    {
        await using var context = await fixture.Browser.NewContextAsync(new()
        {
            IgnoreHTTPSErrors = true,
            ViewportSize = new() { Width = width, Height = 800 }
        });
        var page = await context.NewPageAsync();

        await page.GotoAsync(fixture.BaseUrl);

        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Sign in", Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("TCMD", new() { Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByLabel("Username")).ToBeFocusedAsync();
        await Assertions.Expect(page.GetByLabel("Username")).ToHaveAttributeAsync("autocomplete", "username");
        await Assertions.Expect(page.GetByLabel("Password")).ToHaveAttributeAsync("autocomplete", "current-password");
        Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth + 1"));
    }

    [Theory]
    [InlineData(320)]
    [InlineData(375)]
    [InlineData(768)]
    [InlineData(1024)]
    [InlineData(1280)]
    public async Task ScreenshotTargetScreens_HaveReachableActionsAndNoCriticalOverflow(int width)
    {
        await using var context = await SignInAsync(width);
        var key = Guid.NewGuid().ToString("N");
        var student = await ApiAsync(context, "/api/students", "POST", new
        {
            fullName = "Amira El Mansouri", phoneNumber = $"+2126{key[..9]}", email = $"amira.{key}@example.test"
        }, 201);
        var course = await ApiAsync(context, "/api/courses", "POST", new
        {
            code = $"PD{Guid.NewGuid():N}"[..18], name = "Digital Operations", description = "Practical operations training"
        }, 201);
        var instructor = await ApiAsync(context, "/api/instructors", "POST", new
        {
            fullName = "Youssef Benali", phoneNumber = $"+2127{key[..9]}", email = $"youssef.{key}@example.test"
        }, 201);
        var group = await ApiAsync(context, "/api/training-groups", "POST", new
        {
            name = "Digital Operations — October", courseId = Id(course), primaryInstructorId = Id(instructor),
            plannedStartDate = "2026-10-05", plannedEndDate = "2026-10-30"
        }, 201);
        var session = await ApiAsync(context, $"/api/training-groups/{Id(group)}/sessions", "POST", new
        {
            sessionDate = "2026-10-06", startTime = "09:00", endTime = "11:00", location = "Training Room A"
        }, 201);
        var account = await ApiAsync(context, "/api/staff-accounts", "POST", new
        {
            userName = $"phase-d-{Guid.NewGuid():N}", displayName = "Nadia Rahal", password = "Password1", role = "Staff"
        }, 201);
        var page = context.Pages.Single();
        var routes = new[]
        {
            "dashboard", "students", $"students/{Id(student)}", $"groups/{Id(group)}",
            $"sessions/{Id(session)}", $"staff-accounts/{Id(account)}"
        };

        foreach (var route in routes)
        {
            await page.GotoAsync($"{fixture.BaseUrl}/#/{route}");
            await Assertions.Expect(page.GetByRole(AriaRole.Main)).ToBeVisibleAsync();
            Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth + 1"),
                $"Critical horizontal overflow at {width}px on {route}");
        }

        await page.GotoAsync($"{fixture.BaseUrl}/#/groups/{Id(group)}");
        await Assertions.Expect(page.GetByRole(AriaRole.Navigation, new() { Name = "Training Group sections" })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("Lifecycle actions", new() { Exact = true })).ToBeVisibleAsync();

        await page.GotoAsync($"{fixture.BaseUrl}/#/sessions/{Id(session)}");
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Attendance roster", Exact = true })).ToBeVisibleAsync();

        await page.GotoAsync($"{fixture.BaseUrl}/#/staff-accounts/{Id(account)}");
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Activation / deactivation", Exact = true })).ToBeVisibleAsync();
    }

    private async Task<IBrowserContext> SignInAsync(int width)
    {
        var context = await fixture.Browser.NewContextAsync(new()
        {
            IgnoreHTTPSErrors = true,
            ViewportSize = new() { Width = width, Height = 900 }
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

    private static string Id(JsonElement item) => item.GetProperty("id").GetString()!;
}
