using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using TCMD.Api.Authentication;
using TCMD.Infrastructure.Identity;
using TCMD.Infrastructure.Persistence;

namespace TCMD.IntegrationTests;

internal static class AuthenticatedClient
{
    public static async Task<HttpClient> CreateAsync(TcmdApiFactory factory, string role = "Staff")
    {
        await EnsureApprovedRolesAsync(factory);
        var (userName, password) = await CreateAccountAsync(factory, role, true);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        var response = await client.PostAsJsonAsync("/api/auth/login", new { userName, password });
        Assert.Equal(System.Net.HttpStatusCode.NoContent, response.StatusCode);
        return client;
    }

    public static async Task<(string UserName, string Password)> CreateAccountAsync(TcmdApiFactory factory, string role, bool active)
    {
        var userName = $"{role.ToLowerInvariant()}-{Guid.NewGuid():N}";
        const string password = "Password1";
        using var scope = factory.Services.CreateScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        if (!await roles.RoleExistsAsync(role)) await roles.CreateAsync(new IdentityRole<Guid>(role));
        var users = scope.ServiceProvider.GetRequiredService<UserManager<StaffUser>>();
        var user = new StaffUser { UserName = userName, DisplayName = "Test User", IsActive = active };
        Assert.True((await users.CreateAsync(user, password)).Succeeded);
        Assert.True((await users.AddToRoleAsync(user, role)).Succeeded);
        return (userName, password);
    }

    public static async Task ClearIdentityAsync(TcmdApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TcmdDbContext>();
        await db.UserRoles.ExecuteDeleteAsync();
        await db.Users.ExecuteDeleteAsync();
        await db.Roles.ExecuteDeleteAsync();
    }

    public static async Task<Guid> FindUserIdAsync(TcmdApiFactory factory, string userName)
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<StaffUser>>();
        return (await users.FindByNameAsync(userName))!.Id;
    }

    private static async Task EnsureApprovedRolesAsync(TcmdApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var role in TcmdPolicies.Roles)
        {
            if (!await roles.RoleExistsAsync(role))
            {
                Assert.True((await roles.CreateAsync(new IdentityRole<Guid>(role))).Succeeded);
            }
        }
    }
}
