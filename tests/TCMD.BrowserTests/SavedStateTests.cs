using System.Text.Json;
using Microsoft.Playwright;

namespace TCMD.BrowserTests;

[Collection(BrowserCollection.Name)]
public sealed class SavedStateTests(BrowserFixture fixture)
{
    [Fact]
    public async Task Session_CanSaveTwice_UsingReturnedVersionAndNormalizedValues_WithoutDocumentReload()
    {
        await using var context = await SignInAsync();
        var session = await CreateSessionAsync(context);
        var path = $"/api/training-sessions/{Id(session)}";
        var page = await OpenAsync(context, $"/sessions/{Id(session)}", "Training Session");
        await page.GetByLabel("Location").FillAsync("  First saved location  ");
        var first = await WriteAsync(page, "Save Session", path, "PUT", Version(session));
        await ExpectDetailAsync(page, "Location", "First saved location");
        await Assertions.Expect(page.GetByLabel("Location")).ToHaveValueAsync("First saved location");
        Assert.NotEqual(Version(session), Version(first));

        await page.GetByLabel("Location").FillAsync("  Second saved location  ");
        var second = await WriteAsync(page, "Save Session", path, "PUT", Version(first));
        await ExpectDetailAsync(page, "Location", "Second saved location");
        await Assertions.Expect(page.GetByLabel("Location")).ToHaveValueAsync("Second saved location");
        Assert.Equal(Version(second), Version(await ApiAsync(context, path)));
        await ExpectSameDocumentAsync(page);
    }

    [Theory]
    [InlineData("complete", "Completed")]
    [InlineData("cancel", "Cancelled")]
    public async Task Session_EditThenStatus_UsesLatestVersionAndUpdatesControls(string action, string status)
    {
        await using var context = await SignInAsync();
        var session = await CreateSessionAsync(context);
        var path = $"/api/training-sessions/{Id(session)}";
        var page = await OpenAsync(context, $"/sessions/{Id(session)}", "Training Session");
        await page.GetByLabel("Location").FillAsync("Saved before status change");
        var edited = await WriteAsync(page, "Save Session", path, "PUT", Version(session));
        var changed = await ChangeStatusAsync(page, action, path, Version(edited));
        Assert.Equal(status, changed.GetProperty("status").GetString());
        await ExpectDetailAsync(page, "Location", "Saved before status change");
        await ExpectDetailAsync(page, "Status", status);
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Save Session", Exact = true })).ToBeDisabledAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Complete", Exact = true })).ToHaveCountAsync(0);
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Cancel", Exact = true })).ToHaveCountAsync(0);
        Assert.Equal(Version(changed), Version(await ApiAsync(context, path)));
        await ExpectSameDocumentAsync(page);
    }

    [Theory]
    [InlineData("complete", "Completed")]
    [InlineData("cancel", "Cancelled")]
    public async Task Group_EditActivateEditThenTerminalStatus_AlwaysUsesLatestVersion(string action, string status)
    {
        await using var context = await SignInAsync();
        var group = await CreateGroupAsync(context);
        var path = $"/api/training-groups/{Id(group)}";
        var page = await OpenAsync(context, $"/groups/{Id(group)}", "Training Group");
        await page.GetByLabel("Group name").FillAsync("  First saved group  ");
        var edited = await WriteAsync(page, "Save changes", path, "PUT", Version(group));
        await ExpectDetailAsync(page, "Name", "First saved group");
        await Assertions.Expect(page.GetByLabel("Group name")).ToHaveValueAsync("First saved group");
        var active = await ChangeStatusAsync(page, "activate", path, Version(edited));
        await ExpectDetailAsync(page, "Status", "Active");
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Activate", Exact = true })).ToHaveCountAsync(0);

        await page.GetByLabel("Group name").FillAsync("  Saved after activation  ");
        var updated = await WriteAsync(page, "Save changes", path, "PUT", Version(active));
        await ExpectDetailAsync(page, "Name", "Saved after activation");
        var terminal = await ChangeStatusAsync(page, action, path, Version(updated));
        await ExpectDetailAsync(page, "Status", status);
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Save changes", Exact = true })).ToBeDisabledAsync();
        Assert.Equal(Version(terminal), Version(await ApiAsync(context, path)));
        await ExpectSameDocumentAsync(page);
    }

    [Theory]
    [InlineData("session", false)]
    [InlineData("session", true)]
    [InlineData("group", false)]
    [InlineData("group", true)]
    [InlineData("course", false)]
    [InlineData("course", true)]
    public async Task StaleWrite_IsRejected_UntilExplicitReload_ThenUsesLatestVersion(string kind, bool statusAction)
    {
        await using var context = await SignInAsync();
        var record = kind == "session" ? await CreateSessionAsync(context)
            : kind == "group" ? await CreateGroupAsync(context) : await CreateCourseAsync(context);
        var apiKind = kind == "session" ? "training-sessions" : kind == "group" ? "training-groups" : "courses";
        var routeKind = kind == "session" ? "sessions" : kind == "group" ? "groups" : "courses";
        var heading = kind == "session" ? "Training Session" : kind == "group" ? "Training Group" : "Course details";
        var field = kind == "session" ? "Location" : kind == "group" ? "Group name" : "Name";
        var save = kind == "session" ? "Save Session" : "Save changes";
        var path = $"/api/{apiKind}/{Id(record)}";
        var page = await OpenAsync(context, $"/{routeKind}/{Id(record)}", heading);
        await page.GetByLabel(field, new() { Exact = true }).FillAsync("Unsaved browser edit");
        var serverValues = record.Deserialize<Dictionary<string, JsonElement>>()!;
        serverValues[kind == "session" ? "location" : "name"] = JsonSerializer.SerializeToElement("Other user's saved value");
        var concurrent = await ApiAsync(context, path, "PUT", serverValues);

        // Deliberately change the human-readable conflict title: recovery must use rowVersion, not English text.
        await page.RouteAsync($"**{path}**", async route =>
        {
            if (route.Request.Method is not ("PUT" or "POST")) { await route.ContinueAsync(); return; }
            var response = await route.FetchAsync();
            if (response.Status == 409)
            {
                var problem = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(await response.TextAsync())!;
                problem["title"] = JsonSerializer.SerializeToElement("Record update rejected");
                await route.FulfillAsync(new() { Response = response, Body = JsonSerializer.Serialize(problem) });
            }
            else await route.FulfillAsync(new() { Response = response });
        });
        var writes = 0;
        page.Request += (_, request) => { if (request.Method is "PUT" or "POST") writes++; };
        if (statusAction)
        {
            var action = kind == "session" ? "complete" : kind == "group" ? "activate" : "deactivate";
            await ChangeStatusAsync(page, action, path, Version(record), 409);
        }
        else await WriteAsync(page, save, path, "PUT", Version(record), 409);
        await Assertions.Expect(page.GetByRole(AriaRole.Alert)).ToContainTextAsync("This record has changed.");
        await Assertions.Expect(page.GetByLabel(field, new() { Exact = true })).ToHaveValueAsync("Unsaved browser edit");
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = save, Exact = true })).ToBeDisabledAsync();
        Assert.Equal(Version(concurrent), Version(await ApiAsync(context, path)));
        Assert.Equal(1, writes); // The failed write was not retried automatically.

        await page.GetByRole(AriaRole.Button, new() { Name = "Reload latest", Exact = true }).ClickAsync();
        if (kind is "group" or "course")
            await page.GetByRole(AriaRole.Button, new() { Name = "Edit details", Exact = true }).ClickAsync();
        await Assertions.Expect(page.GetByLabel(field, new() { Exact = true })).ToHaveValueAsync("Other user's saved value");
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = save, Exact = true })).ToBeEnabledAsync();
        await page.GetByLabel(field, new() { Exact = true }).FillAsync("Reviewed and saved");
        var recovered = await WriteAsync(page, save, path, "PUT", Version(concurrent));
        Assert.Equal("Reviewed and saved", recovered.GetProperty(kind == "session" ? "location" : "name").GetString());
        Assert.Equal(Version(recovered), Version(await ApiAsync(context, path)));
    }

    [Fact]
    public async Task BusinessConflict_WithUnchangedVersion_DoesNotLockTheEditor()
    {
        await using var context = await SignInAsync();
        var group = await CreateGroupAsync(context, false);
        var path = $"/api/training-groups/{Id(group)}";
        var page = await OpenAsync(context, $"/groups/{Id(group)}", "Training Group");
        await ChangeStatusAsync(page, "activate", path, Version(group), 409);
        await Assertions.Expect(page.GetByRole(AriaRole.Alert)).ToContainTextAsync("A primary instructor is required before activation.");
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Save changes", Exact = true })).ToBeEnabledAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Reload latest", Exact = true })).ToHaveCountAsync(0);
        await page.GetByLabel("Group name").FillAsync("Still editable");
        await WriteAsync(page, "Save changes", path, "PUT", Version(group));
        await ExpectDetailAsync(page, "Name", "Still editable");
    }

    [Fact]
    public async Task ConflictVersionCheckAndReloadFailure_KeepWritesLockedUntilSuccessfulReload()
    {
        await using var context = await SignInAsync();
        var session = await CreateSessionAsync(context);
        var path = $"/api/training-sessions/{Id(session)}";
        var page = await OpenAsync(context, $"/sessions/{Id(session)}", "Training Session");
        var values = session.Deserialize<Dictionary<string, JsonElement>>()!;
        values["location"] = JsonSerializer.SerializeToElement("Concurrent location");
        var concurrent = await ApiAsync(context, path, "PUT", values);
        await page.RouteAsync($"**{path}", async route =>
        {
            if (route.Request.Method == "GET") await route.AbortAsync();
            else await route.ContinueAsync();
        });
        await page.GetByLabel("Location").FillAsync("Pending location");
        await WriteAsync(page, "Save Session", path, "PUT", Version(session), 409);
        await Assertions.Expect(page.GetByRole(AriaRole.Alert)).ToContainTextAsync("The current version could not be checked.");
        await page.GetByRole(AriaRole.Button, new() { Name = "Reload latest", Exact = true }).ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Alert)).ToContainTextAsync("TCMD could not reach the server.");
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Save Session", Exact = true })).ToBeDisabledAsync();
        await page.UnrouteAsync($"**{path}");
        await page.GetByRole(AriaRole.Button, new() { Name = "Reload latest", Exact = true }).ClickAsync();
        await Assertions.Expect(page.GetByLabel("Location")).ToHaveValueAsync("Concurrent location");
        await page.GetByLabel("Location").FillAsync("Recovered location");
        await WriteAsync(page, "Save Session", path, "PUT", Version(concurrent));
        await ExpectDetailAsync(page, "Location", "Recovered location");
        await ExpectSameDocumentAsync(page);
    }

    private async Task<IBrowserContext> SignInAsync()
    {
        var context = await fixture.Browser.NewContextAsync(new() { IgnoreHTTPSErrors = true });
        try
        {
            await ApiAsync(context, "/api/auth/login", "POST",
                new { userName = BrowserFixture.AdminUserName, password = BrowserFixture.AdminPassword }, 204);
            return context;
        }
        catch { await context.DisposeAsync(); throw; }
    }

    private async Task<IPage> OpenAsync(IBrowserContext context, string route, string heading)
    {
        var page = await context.NewPageAsync();
        await page.GotoAsync($"{fixture.BaseUrl}/#{route}");
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = heading, Exact = true })).ToBeVisibleAsync();
        if (route.StartsWith("/groups/") || route.StartsWith("/sessions/") || route.StartsWith("/courses/"))
            await page.GetByRole(AriaRole.Button, new() { Name = "Edit details", Exact = true }).ClickAsync();
        await page.EvaluateAsync("window.savedStateDocument = 'original'");
        return page;
    }

    private static async Task ExpectSameDocumentAsync(IPage page) =>
        Assert.Equal("original", await page.EvaluateAsync<string?>("window.savedStateDocument"));

    private static Task ExpectDetailAsync(IPage page, string label, string value) =>
        Assertions.Expect(page.Locator(".details dl").Filter(new() { Has = page.GetByText(label, new() { Exact = true }) }).Locator("dd"))
            .ToHaveTextAsync(value);

    private static async Task<JsonElement> WriteAsync(IPage page, string button, string path, string method,
        string expectedVersion, int status = 200, bool confirmation = false)
    {
        var response = await page.RunAndWaitForResponseAsync(
            () => (confirmation ? page.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Button, new() { Name = button, Exact = true })
                : page.GetByRole(AriaRole.Button, new() { Name = button, Exact = true })).ClickAsync(),
            response => response.Request.Method == method && new Uri(response.Url).AbsolutePath == path);
        using var body = JsonDocument.Parse(response.Request.PostData!);
        Assert.Equal(expectedVersion, body.RootElement.GetProperty("rowVersion").GetString());
        Assert.True(response.Status == status, $"{method} {path}: expected {status}, got {response.Status}: {await response.TextAsync()}");
        using var document = JsonDocument.Parse(await response.TextAsync());
        if (status == 200) await Assertions.Expect(page.GetByRole(AriaRole.Status).Filter(new() { HasText = "Changes saved." })).ToBeVisibleAsync();
        return document.RootElement.Clone();
    }

    private static async Task<JsonElement> ChangeStatusAsync(IPage page, string action, string path,
        string version, int status = 200)
    {
        await page.GetByRole(AriaRole.Button, new() { Name = char.ToUpperInvariant(action[0]) + action[1..], Exact = true }).ClickAsync();
        // Entity deactivation retains its existing capitalized confirmation label.
        var confirm = action == "deactivate" ? "Deactivate" : action;
        return await WriteAsync(page, confirm, $"{path}/{action}", "POST", version, status, true);
    }

    private async Task<JsonElement> ApiAsync(IBrowserContext context, string path, string method = "GET",
        object? body = null, int status = 200)
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
        var response = await context.APIRequest.FetchAsync($"{fixture.BaseUrl}{path}", new() { Method = method, DataObject = body, Headers = headers });
        var text = await response.TextAsync();
        Assert.True(response.Status == status, $"{method} {path}: expected {status}, got {response.Status}: {text}");
        await response.DisposeAsync();
        if (status == 204) return default;
        using var document = JsonDocument.Parse(text);
        return document.RootElement.Clone();
    }

    private Task<JsonElement> CreateCourseAsync(IBrowserContext context) =>
        ApiAsync(context, "/api/courses", "POST", new { code = $"S{Guid.NewGuid():N}", name = "State course", description = "Preserved description" }, 201);

    private async Task<JsonElement> CreateGroupAsync(IBrowserContext context, bool assigned = true)
    {
        var course = await CreateCourseAsync(context);
        var instructor = assigned ? await ApiAsync(context, "/api/instructors", "POST", new { fullName = "State instructor" }, 201) : default;
        return await ApiAsync(context, "/api/training-groups", "POST", new
        {
            name = $"State group {Guid.NewGuid():N}", courseId = Id(course), primaryInstructorId = assigned ? Id(instructor) : null,
            plannedStartDate = DateTime.UtcNow.AddDays(2).ToString("yyyy-MM-dd"), plannedEndDate = DateTime.UtcNow.AddDays(10).ToString("yyyy-MM-dd")
        }, 201);
    }

    private async Task<JsonElement> CreateSessionAsync(IBrowserContext context)
    {
        var group = await CreateGroupAsync(context);
        return await ApiAsync(context, $"/api/training-groups/{Id(group)}/sessions", "POST", new
        {
            sessionDate = group.GetProperty("plannedStartDate").GetString(), startTime = "09:00", endTime = "10:00", location = "Initial location"
        }, 201);
    }

    private static string Id(JsonElement record) => record.GetProperty("id").GetString()!;
    private static string Version(JsonElement record) => record.GetProperty("rowVersion").GetString()!;
}
