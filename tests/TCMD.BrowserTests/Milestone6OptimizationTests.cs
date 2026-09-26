using System.Text.Json;
using Microsoft.Playwright;

namespace TCMD.BrowserTests;

[Collection(BrowserCollection.Name)]
public sealed class Milestone6OptimizationTests(BrowserFixture fixture)
{
    [Fact]
    public async Task GroupTabs_ReuseStableReads_AndPreserveUnsavedFormValues()
    {
        await using var context = await SignInAsync();
        var course = await ApiAsync(context, "/api/courses", "POST", new
        {
            code = $"M6{Guid.NewGuid():N}"[..20], name = "Milestone 6 course", description = ""
        }, 201);
        var group = await ApiAsync(context, "/api/training-groups", "POST", new
        {
            name = "Milestone 6 group", courseId = Id(course),
            plannedStartDate = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            plannedEndDate = DateTime.UtcNow.AddDays(2).ToString("yyyy-MM-dd")
        }, 201);
        var page = context.Pages.Single();
        var groupReads = 0;
        var courseReads = 0;
        var instructorReads = 0;
        await page.RouteAsync("**/api/**", async route =>
        {
            var uri = new Uri(route.Request.Url);
            if (route.Request.Method == "GET" && uri.AbsolutePath == $"/api/training-groups/{Id(group)}") groupReads++;
            if (route.Request.Method == "GET" && uri.AbsolutePath == "/api/courses") courseReads++;
            if (route.Request.Method == "GET" && uri.AbsolutePath == "/api/instructors") instructorReads++;
            await route.ContinueAsync();
        });

        await page.GotoAsync($"{fixture.BaseUrl}/#/groups/{Id(group)}");
        await page.GetByLabel("Group name").FillAsync("unsaved tab value");
        await page.GetByRole(AriaRole.Link, new() { Name = "Sessions", Exact = true }).ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Training Sessions", Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByLabel("Group name")).ToHaveValueAsync("unsaved tab value");
        await page.GetByRole(AriaRole.Link, new() { Name = "Attendance", Exact = true }).ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Attendance history", Exact = true })).ToBeVisibleAsync();

        Assert.Equal(1, groupReads);
        Assert.Equal(1, courseReads);
        Assert.Equal(1, instructorReads);
    }

    [Fact]
    public async Task StudentHistories_StartInParallel()
    {
        await using var context = await SignInAsync();
        var student = await ApiAsync(context, "/api/students", "POST", new
        {
            fullName = $"Parallel {Guid.NewGuid():N}", phoneNumber = "+212600000001"
        }, 201);
        var page = context.Pages.Single();
        var enrollmentStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var attendanceStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await page.RouteAsync($"**/api/students/{Id(student)}/enrollments", route => HoldAsync(route, enrollmentStarted, release));
        await page.RouteAsync($"**/api/students/{Id(student)}/attendance", route => HoldAsync(route, attendanceStarted, release));

        var navigation = page.GotoAsync($"{fixture.BaseUrl}/#/students/{Id(student)}");
        var bothStarted = Task.WhenAll(enrollmentStarted.Task, attendanceStarted.Task);
        Assert.Same(bothStarted, await Task.WhenAny(bothStarted, Task.Delay(TimeSpan.FromSeconds(5))));
        release.SetResult();
        await navigation;
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Enrollment history", Exact = true })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task EntityList_RestoresSubmittedFiltersAfterReturningFromDetail()
    {
        await using var context = await SignInAsync();
        var student = await ApiAsync(context, "/api/students", "POST", new
        {
            fullName = $"Retained filter {Guid.NewGuid():N}", phoneNumber = "+212600000002"
        }, 201);
        var page = context.Pages.Single();
        await page.GotoAsync($"{fixture.BaseUrl}/#/students");
        await page.GetByLabel("Search").FillAsync("Retained filter");
        await page.GetByLabel("Status").SelectOptionAsync("true");
        await page.GetByRole(AriaRole.Button, new() { Name = "Search", Exact = true }).ClickAsync();
        await page.GetByRole(AriaRole.Link, new() { Name = $"View {student.GetProperty("studentNumber").GetString()}", Exact = true }).ClickAsync();
        await page.GetByRole(AriaRole.Link, new() { Name = "Back to list", Exact = true }).ClickAsync();

        await Assertions.Expect(page.GetByLabel("Search")).ToHaveValueAsync("Retained filter");
        await Assertions.Expect(page.GetByLabel("Status")).ToHaveValueAsync("true");
    }

    private static async Task HoldAsync(IRoute route, TaskCompletionSource started, TaskCompletionSource release)
    {
        started.SetResult();
        await release.Task;
        await route.ContinueAsync();
    }

    private async Task<IBrowserContext> SignInAsync()
    {
        var context = await fixture.Browser.NewContextAsync(new() { IgnoreHTTPSErrors = true });
        await ApiAsync(context, "/api/auth/login", "POST",
            new { userName = BrowserFixture.AdminUserName, password = BrowserFixture.AdminPassword }, 204);
        await context.NewPageAsync();
        return context;
    }

    private async Task<JsonElement> ApiAsync(IBrowserContext context, string path, string method = "GET",
        object? body = null, int expectedStatus = 200)
    {
        var headers = new Dictionary<string, string>();
        if (method != "GET")
        {
            var security = await context.APIRequest.GetAsync($"{fixture.BaseUrl}/api/auth/antiforgery");
            using var tokens = JsonDocument.Parse(await security.TextAsync());
            headers["X-CSRF-TOKEN"] = tokens.RootElement.GetProperty("requestToken").GetString()!;
            await security.DisposeAsync();
        }
        var response = await context.APIRequest.FetchAsync($"{fixture.BaseUrl}{path}",
            new() { Method = method, DataObject = body, Headers = headers });
        var content = await response.TextAsync();
        Assert.True(response.Status == expectedStatus,
            $"{method} {path}: expected {expectedStatus}, got {response.Status}: {content}");
        await response.DisposeAsync();
        if (expectedStatus == 204) return default;
        using var document = JsonDocument.Parse(content);
        return document.RootElement.Clone();
    }

    private static string Id(JsonElement item) => item.GetProperty("id").GetString()!;
}
