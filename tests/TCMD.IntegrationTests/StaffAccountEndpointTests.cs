using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;

namespace TCMD.IntegrationTests;

[Collection(DatabaseIntegrationCollection.Name)]
public sealed class StaffAccountEndpointTests(TcmdApiFactory factory)
{
    [Fact]
    public async Task Administrator_CanCreateListAndRetrieveAccount()
    {
        using var admin = await AuthenticatedClient.CreateAsync(factory, "Administrator");
        var userName = $"created-{Guid.NewGuid():N}";
        var create = await admin.PostAsJsonAsync("/api/staff-accounts", new { userName, displayName = "Created User", password = "Password1", role = "Staff" });
        var account = await create.Content.ReadFromJsonAsync<AccountResponse>();
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.NotNull(account);
        Assert.Equal("Staff", account.Role);
        Assert.True(account.IsActive);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/staff-accounts/{account.Id}")).StatusCode);
        var list = await admin.GetFromJsonAsync<List<AccountResponse>>("/api/staff-accounts");
        Assert.Contains(list!, item => item.Id == account.Id);
    }

    [Theory]
    [InlineData("Staff")]
    [InlineData("Instructor")]
    public async Task NonAdministrator_CannotManageAccounts(string role)
    {
        using var client = await AuthenticatedClient.CreateAsync(factory, role);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/staff-accounts")).StatusCode);
    }

    [Fact]
    public async Task Create_RejectsInvalidDuplicateAndMissingValues()
    {
        using var admin = await AuthenticatedClient.CreateAsync(factory, "Administrator");
        var name = $"duplicate-{Guid.NewGuid():N}";
        var valid = new { userName = name, displayName = "User", password = "Password1", role = "Staff" };
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync("/api/staff-accounts", valid)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/staff-accounts", valid)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/staff-accounts", new { userName = "", displayName = "", password = "", role = "" })).StatusCode);
        await AssertValidationErrorAsync(
            await admin.PostAsJsonAsync("/api/staff-accounts", new { userName = $"bad-password-{Guid.NewGuid():N}", displayName = "Bad", password = "short", role = "Staff" }),
            "password");
        await AssertValidationErrorAsync(
            await admin.PostAsJsonAsync("/api/staff-accounts", new { userName = $"bad-role-{Guid.NewGuid():N}", displayName = "Bad", password = "Password1", role = "Owner" }),
            "role");
        await AssertValidationErrorAsync(
            await admin.PostAsJsonAsync("/api/staff-accounts", new { userName = new string('a', 257), displayName = "Bad", password = "Password1", role = "Staff" }),
            "userName");
        await AssertValidationErrorAsync(
            await admin.PostAsJsonAsync("/api/staff-accounts", new { userName = "unsupported!name", displayName = "Bad", password = "Password1", role = "Staff" }),
            "userName");
    }

    [Fact]
    public async Task MissingAccount_ReturnsNotFound()
    {
        using var admin = await AuthenticatedClient.CreateAsync(factory, "Administrator");
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/staff-accounts/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task Deactivation_InvalidatesSession_AndSelfDeactivationIsRejected()
    {
        var (adminName, _) = await AuthenticatedClient.CreateAccountAsync(factory, "Administrator", true);
        using var admin = await LoginAsync(adminName, "Password1");
        var adminId = await AuthenticatedClient.FindUserIdAsync(factory, adminName);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PatchAsJsonAsync($"/api/staff-accounts/{adminId}/active", new { isActive = false })).StatusCode);
        var (staffName, _) = await AuthenticatedClient.CreateAccountAsync(factory, "Staff", true);
        using var staff = await LoginAsync(staffName, "Password1");
        var staffId = await AuthenticatedClient.FindUserIdAsync(factory, staffName);
        Assert.Equal(HttpStatusCode.OK, (await admin.PatchAsJsonAsync($"/api/staff-accounts/{staffId}/active", new { isActive = false })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await staff.GetAsync($"/api/students/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PatchAsJsonAsync($"/api/staff-accounts/{staffId}/active", new { isActive = true })).StatusCode);
        using var reactivated = await LoginAsync(staffName, "Password1");
        Assert.Equal(HttpStatusCode.NotFound, (await reactivated.GetAsync($"/api/students/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task LastActiveAdministrator_CannotBeDemoted()
    {
        await AuthenticatedClient.ClearIdentityAsync(factory);
        var (name, _) = await AuthenticatedClient.CreateAccountAsync(factory, "Administrator", true);
        using var admin = await LoginAsync(name, "Password1");
        var id = await AuthenticatedClient.FindUserIdAsync(factory, name);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PatchAsJsonAsync($"/api/staff-accounts/{id}/role", new { role = "Staff" })).StatusCode);
    }

    [Fact]
    public async Task RoleAndPasswordChanges_InvalidateSessionAndTakeEffect()
    {
        using var admin = await AuthenticatedClient.CreateAsync(factory, "Administrator");
        var (staffName, _) = await AuthenticatedClient.CreateAccountAsync(factory, "Staff", true);
        var staffId = await AuthenticatedClient.FindUserIdAsync(factory, staffName);
        using var oldSession = await LoginAsync(staffName, "Password1");
        Assert.Equal(HttpStatusCode.OK, (await admin.PatchAsJsonAsync($"/api/staff-accounts/{staffId}/role", new { role = "Instructor" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await oldSession.GetAsync($"/api/students/{Guid.NewGuid()}")).StatusCode);
        using var instructor = await LoginAsync(staffName, "Password1");
        Assert.Equal(HttpStatusCode.Forbidden, (await instructor.GetAsync($"/api/students/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/api/staff-accounts/{staffId}/password", new { newPassword = "Replacement1" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginResponseAsync(staffName, "Password1")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await LoginResponseAsync(staffName, "Replacement1")).StatusCode);
        await AssertValidationErrorAsync(
            await admin.PostAsJsonAsync($"/api/staff-accounts/{staffId}/password", new { newPassword = "short" }),
            "newPassword");
    }

    private async Task<HttpClient> LoginAsync(string userName, string password)
    {
        var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/auth/login", new { userName, password })).StatusCode);
        return client;
    }

    private async Task<HttpResponseMessage> LoginResponseAsync(string userName, string password)
    {
        var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        return await client.PostAsJsonAsync("/api/auth/login", new { userName, password });
    }

    private static async Task AssertValidationErrorAsync(HttpResponseMessage response, string field)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains(field, problem.Errors);
    }

    private sealed record AccountResponse(Guid Id, string UserName, string DisplayName, string Role, bool IsActive);
}
