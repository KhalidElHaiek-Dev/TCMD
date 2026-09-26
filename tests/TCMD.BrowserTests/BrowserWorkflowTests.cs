using Microsoft.Playwright;
using System.Collections.Concurrent;

namespace TCMD.BrowserTests;

[Collection(BrowserCollection.Name)]
public sealed class BrowserWorkflowTests(BrowserFixture fixture)
{
    [Fact]
    public async Task Administrator_CanCompleteRepresentativeOperationalAndAccessWorkflows()
    {
        await using var context = await fixture.Browser.NewContextAsync(new() { IgnoreHTTPSErrors = true, ViewportSize = new() { Width = 1280, Height = 800 } });
        var page = await context.NewPageAsync();
        var diagnostics = CaptureDiagnostics(page);
        await context.Tracing.StartAsync(new() { Screenshots = true, Snapshots = true, Sources = true });
        try
        {
        await page.GotoAsync(fixture.BaseUrl, new() { WaitUntil = WaitUntilState.DOMContentLoaded });
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await ExpectInitialLoginAsync(page, diagnostics);
        await page.GetByLabel("Username").FillAsync(BrowserFixture.AdminUserName);
        await page.GetByLabel("Password").FillAsync(BrowserFixture.AdminPassword);
        await page.GetByRole(AriaRole.Button, new() { Name = "Sign in" }).ClickAsync();
        await ExpectHeadingWithDiagnostics(page, "Welcome, Browser Administrator", diagnostics,
            "post-login-dashboard-failure.png");
        await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "Staff Accounts" })).ToBeVisibleAsync();

        var suffix=Guid.NewGuid().ToString("N")[..8];
        await page.GetByRole(AriaRole.Link,new(){Name="Students"}).ClickAsync();
        await page.GetByRole(AriaRole.Link,new(){Name="Add Student"}).ClickAsync();
        await page.GetByLabel("Full name").FillAsync($"Browser Student {suffix}");
        await page.GetByLabel("Phone number").FillAsync("+212600000000");
        await page.GetByRole(AriaRole.Button,new(){Name="Create Student"}).ClickAsync();
        await ExpectHeading(page,"Student details");
        await Assertions.Expect(page.GetByText("Active",new(){Exact=true})).ToBeVisibleAsync();
        var studentId = page.Url.Split("#/students/")[1];
        await page.GetByLabel("Full name").FillAsync($"Attempted Student {suffix}");
        await page.EvaluateAsync("""async id => { const token=(await (await fetch('/api/auth/antiforgery')).json()).requestToken; const current=await (await fetch('/api/students/'+id)).json(); await fetch('/api/students/'+id,{method:'PUT',headers:{'Content-Type':'application/json','X-CSRF-TOKEN':token},body:JSON.stringify({...current,fullName:'Concurrent Student'})}); }""", studentId);
        await page.GetByRole(AriaRole.Button,new(){Name="Save changes"}).ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Alert).Filter(new() { HasText = "Student was changed by another user. Reload and try again." })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button,new(){Name="Reload latest"})).ToBeVisibleAsync();

        await page.GetByRole(AriaRole.Link,new(){Name="Courses"}).ClickAsync();
        await page.GetByRole(AriaRole.Link,new(){Name="Add Course"}).ClickAsync();
        await page.GetByLabel("Code").FillAsync($"B{suffix}");await page.GetByLabel("Name").FillAsync("Browser Course");
        await page.GetByRole(AriaRole.Button,new(){Name="Create Course"}).ClickAsync();await ExpectHeading(page,"Course details");
        var courseId = page.Url.Split("#/courses/")[1];

        await page.GetByRole(AriaRole.Link,new(){Name="Instructors"}).ClickAsync();
        await page.GetByRole(AriaRole.Link,new(){Name="Add Instructor"}).ClickAsync();
        await page.GetByLabel("Full name").FillAsync($"Browser Instructor {suffix}");
        await page.GetByRole(AriaRole.Button,new(){Name="Create Instructor"}).ClickAsync();await ExpectHeading(page,"Instructor details");
        var instructorId = page.Url.Split("#/instructors/")[1];

        await page.GetByRole(AriaRole.Link,new(){Name="Training Groups"}).ClickAsync();
        await page.GetByRole(AriaRole.Link,new(){Name="Add Training Group"}).ClickAsync();
        await page.GetByLabel("Group name").FillAsync($"Browser Group {suffix}");
        await page.GetByLabel("Course").SelectOptionAsync(courseId);
        await page.GetByLabel("Primary Instructor").SelectOptionAsync(instructorId);
        var today=DateTime.Today.ToString("yyyy-MM-dd");await page.GetByLabel("Planned start").FillAsync(today);await page.GetByLabel("Planned end").FillAsync(today);
        await page.GetByRole(AriaRole.Button,new(){Name="Create Group"}).ClickAsync();await ExpectHeading(page,"Training Group");
        await page.GetByRole(AriaRole.Link,new(){Name="Enrollments"}).ClickAsync();
        await SelectActiveStudentAsync(page, studentId, $"Browser Student {suffix}", diagnostics);
        await page.GetByRole(AriaRole.Button,new(){Name="Enroll Student"}).ClickAsync();
        await page.GetByRole(AriaRole.Link,new(){Name="Sessions"}).ClickAsync();
        await page.GetByRole(AriaRole.Link,new(){Name="Schedule Session"}).ClickAsync();
        await page.GetByLabel("Session date").FillAsync(today);await page.GetByLabel("Start time").FillAsync("00:01");await page.GetByLabel("End time").FillAsync("23:59");
        await page.GetByRole(AriaRole.Button,new(){Name="Schedule Session"}).ClickAsync();await ExpectHeading(page,"Training Session");
        var attendanceStatus = page.GetByLabel("Attendance status for Concurrent Student");
        await Assertions.Expect(attendanceStatus).ToBeVisibleAsync();
        await Assertions.Expect(attendanceStatus).ToBeEnabledAsync();
        Assert.Equal(["Select", "Present", "Absent", "Late", "Excused"],
            await attendanceStatus.Locator("option").AllTextContentsAsync());
        await attendanceStatus.SelectOptionAsync("Present");
        await Assertions.Expect(attendanceStatus).ToHaveValueAsync("Present");
        var attendanceResponse = await page.RunAndWaitForResponseAsync(
            () => page.GetByRole(AriaRole.Button,new(){Name="Record attendance for Concurrent Student"}).ClickAsync(),
            response => response.Request.Method == "POST" && response.Url.Contains("/attendance"));
        Assert.Equal(201, attendanceResponse.Status);
        await Assertions.Expect(page.GetByRole(AriaRole.Cell,new(){Name="Present",Exact=true})).ToBeVisibleAsync();

        await page.GetByRole(AriaRole.Link,new(){Name="Staff Accounts"}).ClickAsync();
        await page.GetByRole(AriaRole.Link,new(){Name="Add Staff Account"}).ClickAsync();
        var instructorUser=$"browser-instructor-{suffix}";
        await page.GetByLabel("Username").FillAsync(instructorUser);await page.GetByLabel("Display name").FillAsync("Browser Instructor Account");
        await page.GetByLabel("Initial password").FillAsync("short");await page.GetByLabel("Role").SelectOptionAsync("Instructor");
        await page.GetByRole(AriaRole.Button,new(){Name="Create Account"}).ClickAsync();
        const string passwordPolicyMessage = "Password does not meet the required policy.";
        await Assertions.Expect(page.GetByRole(AriaRole.Alert)).ToContainTextAsync(passwordPolicyMessage);
        var initialPassword = page.GetByLabel("Initial password");
        await Assertions.Expect(initialPassword).ToHaveAttributeAsync("aria-invalid", "true");
        var passwordDescriptionIds = (await initialPassword.GetAttributeAsync("aria-describedby"))?.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Assert.NotNull(passwordDescriptionIds);
        Assert.Equal(2, passwordDescriptionIds.Length);
        await Assertions.Expect(page.Locator($"#{passwordDescriptionIds[1]}")).ToHaveTextAsync(passwordPolicyMessage);
        await page.GetByLabel("Initial password").FillAsync("Password1");await page.GetByRole(AriaRole.Button,new(){Name="Create Account"}).ClickAsync();
        await ExpectHeading(page,"Staff Account");await page.GetByLabel("Linked Instructor").SelectOptionAsync(instructorId);
        await page.GetByRole(AriaRole.Button,new(){Name="Update link"}).ClickAsync();
        var linkResponse = await page.RunAndWaitForResponseAsync(
            () => page.GetByRole(AriaRole.Button,new(){Name="Confirm"}).ClickAsync(),
            response => response.Request.Method == "PUT" && response.Url.EndsWith("/instructor-link"));
        Assert.Equal(200, linkResponse.Status);
        await ExpectHeading(page,"Staff Account");
        await Assertions.Expect(page.GetByRole(AriaRole.Status).Filter(new(){HasText="Instructor link updated."})).ToBeVisibleAsync();
        var logoutResponse = await page.RunAndWaitForResponseAsync(
            () => page.GetByRole(AriaRole.Button,new(){Name="Sign out"}).ClickAsync(),
            response => response.Request.Method == "POST" && response.Url.EndsWith("/api/auth/logout"));
        if (logoutResponse.Status != 204)
            Assert.Fail($"Logout returned {logoutResponse.Status}: {await TryReadResponseBodyAsync(logoutResponse)}");
        var logoutHeaders = await logoutResponse.Request.AllHeadersAsync();
        Assert.True(logoutHeaders.TryGetValue("x-csrf-token", out var logoutToken));
        Assert.False(string.IsNullOrWhiteSpace(logoutToken));
        Assert.False(logoutHeaders.ContainsKey("content-type"));
        await ExpectHeading(page,"Sign in to TCMD");
        await page.GetByLabel("Username").FillAsync(instructorUser);await page.GetByLabel("Password").FillAsync("Password1");await page.GetByRole(AriaRole.Button,new(){Name="Sign in"}).ClickAsync();
        await ExpectHeading(page,"Welcome, Browser Instructor Account");
        await Assertions.Expect(page.GetByRole(AriaRole.Link,new(){Name="My Groups"})).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Link,new(){Name="Students"})).ToHaveCountAsync(0);
        await page.GetByRole(AriaRole.Link,new(){Name="My Groups"}).ClickAsync();
        await Assertions.Expect(page.GetByText($"Browser Group {suffix}",new(){Exact=true})).ToBeVisibleAsync();

        await page.EvaluateAsync("""async () => { const token=(await (await fetch('/api/auth/antiforgery')).json()).requestToken; await fetch('/api/auth/logout',{method:'POST',headers:{'X-CSRF-TOKEN':token}}); }""");
        await page.GetByRole(AriaRole.Button,new(){Name="Search"}).ClickAsync();
        await ExpectHeading(page,"Sign in to TCMD");
        await Assertions.Expect(page.GetByText("Your session expired. Sign in again.")).ToBeVisibleAsync();

        await page.SetViewportSizeAsync(320,700);
        await Assertions.Expect(page.GetByRole(AriaRole.Main)).ToBeVisibleAsync();
        await page.Keyboard.PressAsync("Tab");
        await Assertions.Expect(page.GetByLabel("Password")).ToBeFocusedAsync();
        await context.Tracing.StopAsync();
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                $"TCMD browser workflow failed.{Environment.NewLine}" +
                await CaptureFailureAsync(page, context, diagnostics), exception);
        }
    }

    private static Task ExpectHeading(IPage page,string name)=>Assertions.Expect(page.GetByRole(AriaRole.Heading,new(){Name=name,Exact=true})).ToBeVisibleAsync();

    private static async Task<string> TryReadResponseBodyAsync(IResponse response)
    {
        try
        {
            return await response.TextAsync();
        }
        catch (PlaywrightException)
        {
            return "<response body unavailable>";
        }
    }

    private static async Task<string> CaptureFailureAsync(IPage page, IBrowserContext context,
        ConcurrentQueue<string> diagnostics)
    {
        var screenshot = Path.Combine(AppContext.BaseDirectory, "browser-workflow-failure.png");
        var trace = Path.Combine(AppContext.BaseDirectory, "browser-workflow-trace.zip");
        string body;
        string title;
        string screenshotResult;
        string traceResult;
        try { body = await page.Locator("body").InnerTextAsync(); }
        catch (Exception error) { body = $"<body unavailable: {error.Message}>"; }
        try { title = await page.TitleAsync(); }
        catch (Exception error) { title = $"<title unavailable: {error.Message}>"; }
        try { await page.ScreenshotAsync(new() { Path = screenshot, FullPage = true }); screenshotResult = screenshot; }
        catch (Exception error) { screenshotResult = $"<screenshot unavailable: {error.Message}>"; }
        try { await context.Tracing.StopAsync(new() { Path = trace }); traceResult = trace; }
        catch (Exception error) { traceResult = $"<trace unavailable: {error.Message}>"; }
        return $"URL: {page.Url}{Environment.NewLine}Title: {title}{Environment.NewLine}" +
            $"Body:{Environment.NewLine}{body}{Environment.NewLine}" +
            $"Browser diagnostics:{Environment.NewLine}{string.Join(Environment.NewLine, diagnostics)}{Environment.NewLine}" +
            $"Screenshot: {screenshotResult}{Environment.NewLine}Trace: {traceResult}";
    }

    private static async Task SelectActiveStudentAsync(IPage page, string studentId, string createdName,
        ConcurrentQueue<string> diagnostics)
    {
        try
        {
            await page.GetByLabel("Active Student").SelectOptionAsync(studentId);
        }
        catch (TimeoutException exception)
        {
            var evidence = await page.EvaluateAsync<string>("""
                async id => {
                  const response = await fetch('/api/students?isActive=true');
                  const students = response.ok ? await response.json() : [];
                  const student = students.find(candidate => candidate.id === id) ?? null;
                  const options = [...document.querySelectorAll('select[name="studentId"] option')]
                    .map(option => ({ value: option.value, text: option.textContent }));
                  return JSON.stringify({ status: response.status, count: students.length, student, options });
                }
                """, studentId);
            var screenshot = Path.Combine(AppContext.BaseDirectory, "active-student-selection-failure.png");
            await page.ScreenshotAsync(new() { Path = screenshot, FullPage = true });
            throw new InvalidOperationException(
                $"The created active Student could not be selected by its stable ID.{Environment.NewLine}" +
                $"URL: {page.Url}{Environment.NewLine}Title: {await page.TitleAsync()}{Environment.NewLine}" +
                $"Created Student: id={studentId}, originalName={createdName}{Environment.NewLine}" +
                $"Student API and select options: {evidence}{Environment.NewLine}" +
                $"Browser diagnostics:{Environment.NewLine}{string.Join(Environment.NewLine, diagnostics)}{Environment.NewLine}" +
                $"Screenshot: {screenshot}", exception);
        }
    }

    private static async Task ExpectHeadingWithDiagnostics(IPage page, string name,
        ConcurrentQueue<string> diagnostics, string screenshotFileName)
    {
        try
        {
            await ExpectHeading(page, name);
        }
        catch (PlaywrightException exception)
        {
            var screenshot = Path.Combine(AppContext.BaseDirectory, screenshotFileName);
            await page.ScreenshotAsync(new() { Path = screenshot, FullPage = true });
            var body = await page.Locator("body").InnerTextAsync();
            throw new InvalidOperationException(
                $"TCMD did not render the expected heading '{name}'.{Environment.NewLine}" +
                $"URL: {page.Url}{Environment.NewLine}Title: {await page.TitleAsync()}{Environment.NewLine}" +
                $"Body:{Environment.NewLine}{body}{Environment.NewLine}" +
                $"Browser diagnostics:{Environment.NewLine}{string.Join(Environment.NewLine, diagnostics)}{Environment.NewLine}" +
                $"Screenshot: {screenshot}", exception);
        }
    }

    private static ConcurrentQueue<string> CaptureDiagnostics(IPage page)
    {
        var messages = new ConcurrentQueue<string>();
        page.Console += (_, message) => messages.Enqueue($"Console {message.Type}: {message.Text}");
        page.PageError += (_, error) => messages.Enqueue($"Page error: {error}");
        page.RequestFailed += (_, request) => messages.Enqueue(
            $"Request failed: {request.Method} {request.Url} — {request.Failure}");
        page.Response += (_, response) =>
        {
            if (response.Url.Contains("/api/") && response.Status >= 400)
                messages.Enqueue($"Response: {response.Request.Method} {response.Url} -> {response.Status} " +
                    $"({response.Headers.GetValueOrDefault("content-type", "content type unavailable")})");
        };
        return messages;
    }

    private static async Task ExpectInitialLoginAsync(IPage page, ConcurrentQueue<string> diagnostics)
    {
        try
        {
            await Assertions.Expect(page.GetByRole(AriaRole.Heading,
                new() { Name = "Sign in to TCMD", Exact = true })).ToBeVisibleAsync(new() { Timeout = 15_000 });
        }
        catch (PlaywrightException exception)
        {
            var screenshot = Path.Combine(AppContext.BaseDirectory, "initial-login-failure.png");
            await page.ScreenshotAsync(new() { Path = screenshot, FullPage = true });
            var body = await page.Locator("body").InnerTextAsync();
            throw new InvalidOperationException(
                $"TCMD login view did not initialize.{Environment.NewLine}" +
                $"URL: {page.Url}{Environment.NewLine}Title: {await page.TitleAsync()}{Environment.NewLine}" +
                $"Body:{Environment.NewLine}{body}{Environment.NewLine}" +
                $"Browser diagnostics:{Environment.NewLine}{string.Join(Environment.NewLine, diagnostics)}{Environment.NewLine}" +
                $"Screenshot: {screenshot}", exception);
        }
    }
}
