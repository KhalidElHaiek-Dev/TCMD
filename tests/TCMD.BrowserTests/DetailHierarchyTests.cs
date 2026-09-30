using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace TCMD.BrowserTests;

[Collection(BrowserCollection.Name)]
public sealed class DetailHierarchyTests(BrowserFixture fixture)
{
    [Fact]
    public async Task Student_DefaultOrderPrioritizesHistory_AndCancelRestoresAndClosesEditor()
    {
        await using var context = await SignInAsync();
        var student = await ApiAsync(context, "/api/students", "POST", new
        {
            fullName = "Hierarchy student", phoneNumber = "+212600000030", email = "hierarchy@example.test"
        }, 201);
        var page = context.Pages.Single();
        await page.GotoAsync($"{fixture.BaseUrl}/#/students/{Id(student)}");

        var edit = page.GetByRole(AriaRole.Button, new() { Name = "Edit details", Exact = true });
        await Assertions.Expect(page.Locator(".entity-header")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator(".overview")).ToBeVisibleAsync();
        await Assertions.Expect(edit).ToHaveAttributeAsync("aria-expanded", "false");
        await Assertions.Expect(page.Locator("#student-edit-region")).ToBeHiddenAsync();
        Assert.True(await IsBeforeAsync(page, ".related-sections section:nth-child(1)", ".edit-disclosure"));
        Assert.True(await IsBeforeAsync(page, ".related-sections section:nth-child(2)", ".edit-disclosure"));
        Assert.True(await IsBeforeAsync(page, ".edit-disclosure", ".danger-zone"));

        await edit.ClickAsync();
        await Assertions.Expect(edit).ToHaveAttributeAsync("aria-expanded", "true");
        await Assertions.Expect(page.GetByLabel("Full name")).ToBeFocusedAsync();
        await page.GetByLabel("Full name").FillAsync("Unsaved student name");
        await page.Locator("#student-edit-region").GetByRole(AriaRole.Button, new() { Name = "Cancel editing", Exact = true }).ClickAsync();
        await Assertions.Expect(page.Locator("#student-edit-region")).ToBeHiddenAsync();
        await Assertions.Expect(edit).ToBeFocusedAsync();

        await edit.ClickAsync();
        await Assertions.Expect(page.GetByLabel("Full name")).ToHaveValueAsync("Hierarchy student");
        var stored = await ApiAsync(context, $"/api/students/{Id(student)}");
        Assert.Equal("Hierarchy student", stored.GetProperty("fullName").GetString());
    }

    [Fact]
    public async Task Group_DefaultOrderPrioritizesOperations_AndDisclosureStateSurvivesTabs()
    {
        await using var context = await SignInAsync();
        var course = await ApiAsync(context, "/api/courses", "POST", new
        {
            code = $"DH{Guid.NewGuid():N}"[..20], name = "Hierarchy course", description = ""
        }, 201);
        var group = await ApiAsync(context, "/api/training-groups", "POST", new
        {
            name = "Hierarchy group", courseId = Id(course),
            plannedStartDate = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            plannedEndDate = DateTime.UtcNow.AddDays(2).ToString("yyyy-MM-dd")
        }, 201);
        var page = context.Pages.Single();
        await page.GotoAsync($"{fixture.BaseUrl}/#/groups/{Id(group)}");

        var edit = page.GetByRole(AriaRole.Button, new() { Name = "Edit details", Exact = true });
        await Assertions.Expect(page.Locator(".entity-header")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator(".overview")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Navigation, new() { Name = "Training Group sections" }).GetByRole(AriaRole.Link)).ToHaveTextAsync(["Enrollments", "Sessions", "Attendance"]);
        await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "Overview", Exact = true })).ToHaveCountAsync(0);
        await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "Enrollments", Exact = true })).ToHaveAttributeAsync("aria-current", "page");
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Enrollments", Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(edit).ToHaveAttributeAsync("aria-expanded", "false");
        await Assertions.Expect(page.Locator("#group-edit-region")).ToBeHiddenAsync();
        Assert.True(await IsBeforeAsync(page, ".tabs", ".edit-disclosure"));
        Assert.True(await IsBeforeAsync(page, ".edit-disclosure", ".lifecycle-panel"));

        await edit.ClickAsync();
        await page.GetByLabel("Group name").FillAsync("Unsaved hierarchy group");
        await page.GetByRole(AriaRole.Link, new() { Name = "Sessions", Exact = true }).ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Training Sessions", Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Edit details", Exact = true })).ToHaveAttributeAsync("aria-expanded", "true");
        await Assertions.Expect(page.GetByLabel("Group name")).ToHaveValueAsync("Unsaved hierarchy group");
    }

    [Fact]
    public async Task Group_LegacyOverviewTab_NormalizesToEnrollments()
    {
        await using var context = await SignInAsync();
        var course = await ApiAsync(context, "/api/courses", "POST", new
        {
            code = $"LG{Guid.NewGuid():N}"[..20], name = "Legacy route course", description = ""
        }, 201);
        var group = await ApiAsync(context, "/api/training-groups", "POST", new
        {
            name = "Legacy route group", courseId = Id(course),
            plannedStartDate = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            plannedEndDate = DateTime.UtcNow.AddDays(2).ToString("yyyy-MM-dd")
        }, 201);
        var page = context.Pages.Single();

        await page.GotoAsync($"{fixture.BaseUrl}/#/groups/{Id(group)}?tab=overview");

        await Assertions.Expect(page).ToHaveURLAsync(new Regex($@"#/groups/{Regex.Escape(Id(group))}\?tab=enrollments$"));
        await Assertions.Expect(page.Locator(".overview")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "Enrollments", Exact = true })).ToHaveAttributeAsync("aria-current", "page");
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Enrollments", Exact = true })).ToBeVisibleAsync();
    }

    [Theory]
    [InlineData(1280)]
    [InlineData(1024)]
    [InlineData(768)]
    [InlineData(375)]
    [InlineData(320)]
    public async Task InstructorStaffAccount_PrioritizesAccessAndLinkBeforeMaintenanceAndLifecycle(int width)
    {
        await using var context = await SignInAsync(width);
        var instructor = await ApiAsync(context, "/api/instructors", "POST", new
        {
            fullName = "Youssef Benali"
        }, 201);
        var account = await ApiAsync(context, "/api/staff-accounts", "POST", new
        {
            userName = width == 1280 ? "instructor.demo" : $"instructor.demo.{width}", displayName = "Demo Instructor",
            password = "Password1", role = "Instructor"
        }, 201);
        await ApiAsync(context, $"/api/staff-accounts/{Id(account)}/instructor-link", "PUT", new
        {
            instructorId = Id(instructor)
        });
        var page = context.Pages.Single();

        await page.GotoAsync($"{fixture.BaseUrl}/#/staff-accounts/{Id(account)}");

        var headings = page.GetByRole(AriaRole.Heading, new() { Level = 2 });
        await Assertions.Expect(headings).ToHaveTextAsync([
            "Overview", "Role & access", "Instructor link", "Password replacement", "Activation / deactivation"
        ]);
        await Assertions.Expect(page.GetByLabel("Linked Instructor")).ToHaveValueAsync(Id(instructor));
        await Assertions.Expect(page.GetByLabel("Linked Instructor").Locator("option:checked")).ToHaveTextAsync("Youssef Benali");
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Change role", Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Update link", Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Replace password", Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Deactivate account", Exact = true })).ToBeVisibleAsync();
        var grid = page.Locator(".account-sections");
        await Assertions.Expect(grid.Locator(".overview")).ToHaveCountAsync(0);
        Assert.True(await IsBeforeAsync(page, ".overview", ".account-sections"));
        var columnCount = await grid.EvaluateAsync<int>("element => getComputedStyle(element).gridTemplateColumns.split(' ').length");
        var positions = await grid.EvaluateAsync<int[][]>("element => [...element.children].map(panel => [panel.offsetTop, panel.offsetLeft])");
        if (width > 768)
        {
            Assert.Equal(2, columnCount);
            Assert.Equal(positions[0][0], positions[1][0]);
            Assert.True(positions[0][1] < positions[1][1]);
            Assert.Equal(positions[2][0], positions[3][0]);
            Assert.True(positions[2][1] < positions[3][1]);
            Assert.True(positions[0][0] < positions[2][0]);
        }
        else
        {
            Assert.Equal(1, columnCount);
            Assert.True(positions.Zip(positions.Skip(1), (first, second) => first[0] < second[0]).All(result => result));
        }
        Assert.False(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth > document.documentElement.clientWidth"));
    }

    [Fact]
    public async Task InstructorStaffAccount_AllFourManagementWorkflowsRemainFunctional()
    {
        await using var context = await SignInAsync();
        var originalInstructor = await ApiAsync(context, "/api/instructors", "POST", new { fullName = $"Original {Guid.NewGuid():N}" }, 201);
        var replacementInstructor = await ApiAsync(context, "/api/instructors", "POST", new { fullName = $"Replacement {Guid.NewGuid():N}" }, 201);
        var account = await ApiAsync(context, "/api/staff-accounts", "POST", new
        {
            userName = $"hierarchy-workflows-{Guid.NewGuid():N}", displayName = "Hierarchy workflows",
            password = "Password1", role = "Instructor"
        }, 201);
        var accountPath = $"/api/staff-accounts/{Id(account)}";
        await ApiAsync(context, $"{accountPath}/instructor-link", "PUT", new { instructorId = Id(originalInstructor) });
        var page = context.Pages.Single();
        await page.GotoAsync($"{fixture.BaseUrl}/#/staff-accounts/{Id(account)}");

        await page.GetByLabel("Linked Instructor").SelectOptionAsync(Id(replacementInstructor));
        await ConfirmAsync(page, "Update link");
        await Assertions.Expect(page.GetByRole(AriaRole.Status)).ToContainTextAsync("Instructor link updated.");
        await Assertions.Expect(page.GetByLabel("Linked Instructor")).ToHaveValueAsync(Id(replacementInstructor));

        await page.GetByLabel("New password").FillAsync("Replacement1");
        await ConfirmAsync(page, "Replace password");
        await Assertions.Expect(page.GetByRole(AriaRole.Status)).ToContainTextAsync("Password replaced.");

        await ConfirmAsync(page, "Deactivate account");
        await Assertions.Expect(page.GetByRole(AriaRole.Status)).ToContainTextAsync("Account deactivated.");
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Activate account", Exact = true })).ToBeVisibleAsync();

        await page.GetByLabel("Role", new() { Exact = true }).SelectOptionAsync("Staff");
        await ConfirmAsync(page, "Change role");
        await Assertions.Expect(page.GetByRole(AriaRole.Status)).ToContainTextAsync("Account role updated.");
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Instructor link", Exact = true })).ToHaveCountAsync(0);
    }

    [Theory]
    [InlineData(320)]
    [InlineData(375)]
    [InlineData(768)]
    public async Task DetailDisclosuresAndLifecycleRemainReachableAtNarrowWidths(int width)
    {
        await using var context = await SignInAsync(width);
        var student = await ApiAsync(context, "/api/students", "POST", new
        {
            fullName = $"Responsive hierarchy {width}", phoneNumber = "+212600000031"
        }, 201);
        var page = context.Pages.Single();
        await page.GotoAsync($"{fixture.BaseUrl}/#/students/{Id(student)}");
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Edit details", Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Deactivate", Exact = true })).ToBeVisibleAsync();
        Assert.False(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth > document.documentElement.clientWidth"));
    }

    [Theory]
    [InlineData(320)]
    [InlineData(375)]
    [InlineData(768)]
    public async Task GroupOperationalTabsRemainReachableAtNarrowWidths(int width)
    {
        await using var context = await SignInAsync(width);
        var course = await ApiAsync(context, "/api/courses", "POST", new
        {
            code = $"RW{width}{Guid.NewGuid():N}"[..20], name = $"Responsive group course {width}", description = ""
        }, 201);
        var group = await ApiAsync(context, "/api/training-groups", "POST", new
        {
            name = $"Responsive group {width}", courseId = Id(course),
            plannedStartDate = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            plannedEndDate = DateTime.UtcNow.AddDays(2).ToString("yyyy-MM-dd")
        }, 201);
        var page = context.Pages.Single();

        await page.GotoAsync($"{fixture.BaseUrl}/#/groups/{Id(group)}");

        var tabs = page.GetByRole(AriaRole.Navigation, new() { Name = "Training Group sections" });
        await Assertions.Expect(tabs.GetByRole(AriaRole.Link)).ToHaveTextAsync(["Enrollments", "Sessions", "Attendance"]);
        await Assertions.Expect(tabs.GetByRole(AriaRole.Link, new() { Name = "Attendance", Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Edit details", Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Lifecycle actions", Exact = true })).ToBeVisibleAsync();
        Assert.True(await tabs.EvaluateAsync<bool>("element => element.scrollWidth >= element.clientWidth"));
        Assert.False(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth > document.documentElement.clientWidth"));
    }

    private static Task<bool> IsBeforeAsync(IPage page, string first, string second) =>
        page.EvaluateAsync<bool>("([first, second]) => Boolean(document.querySelector(first).compareDocumentPosition(document.querySelector(second)) & Node.DOCUMENT_POSITION_FOLLOWING)", new[] { first, second });

    private static async Task ConfirmAsync(IPage page, string action)
    {
        await page.GetByRole(AriaRole.Button, new() { Name = action, Exact = true }).ClickAsync();
        await page.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Button, new() { Name = "Confirm", Exact = true }).ClickAsync();
    }

    private async Task<IBrowserContext> SignInAsync(int width = 1280)
    {
        var context = await fixture.Browser.NewContextAsync(new() { IgnoreHTTPSErrors = true, ViewportSize = new() { Width = width, Height = 900 } });
        await ApiAsync(context, "/api/auth/login", "POST",
            new { userName = BrowserFixture.AdminUserName, password = BrowserFixture.AdminPassword }, 204);
        await context.NewPageAsync();
        return context;
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
        var text = await response.TextAsync();
        Assert.True(response.Status == status, $"{method} {path}: expected {status}, got {response.Status}: {text}");
        await response.DisposeAsync();
        if (status == 204) return default;
        using var document = JsonDocument.Parse(text);
        return document.RootElement.Clone();
    }

    private static string Id(JsonElement record) => record.GetProperty("id").GetString()!;
}
