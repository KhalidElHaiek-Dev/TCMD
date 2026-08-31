using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TCMD.Api.Authentication;
using TCMD.Infrastructure.Persistence;

namespace TCMD.IntegrationTests;

[Collection(DatabaseIntegrationCollection.Name)]
public sealed class AuthenticationEndpointTests(TcmdApiFactory factory)
{
    [Theory]
    [InlineData("Administrator")]
    [InlineData("Staff")]
    [InlineData("Instructor")]
    public async Task Login_WithActiveAccountForEachRole_ReturnsNoContent(string role)
    {
        var (userName, password) = await AuthenticatedClient.CreateAccountAsync(factory, role, true);
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/auth/login", new { userName, password })).StatusCode);
    }

    [Fact]
    public async Task LoginFailures_AreGeneric_AndLockOutAfterFiveAttempts()
    {
        var (userName, password) = await AuthenticatedClient.CreateAccountAsync(factory, "Staff", true);
        using var client = factory.CreateClient();
        var unknown = await client.PostAsJsonAsync("/api/auth/login", new { userName = "missing", password });
        var incorrect = await client.PostAsJsonAsync("/api/auth/login", new { userName, password = "Wrongpass1" });
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        Assert.Equal("application/problem+json", unknown.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.Unauthorized, incorrect.StatusCode);
        for (var attempt = 0; attempt < 4; attempt++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new { userName, password = "Wrongpass1" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new { userName, password })).StatusCode);
    }

    [Fact]
    public async Task InactiveAccount_IsRejected_AndLogoutEndsSession()
    {
        var (inactiveName, password) = await AuthenticatedClient.CreateAccountAsync(factory, "Staff", false);
        using var inactive = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await inactive.PostAsJsonAsync("/api/auth/login", new { userName = inactiveName, password })).StatusCode);
        using var client = await AuthenticatedClient.CreateAsync(factory);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync($"/api/students/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task StudentEndpoints_RequireAuthentication_AndRejectInstructor()
    {
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/api/students/{Guid.NewGuid()}")).StatusCode);
        using var instructor = await AuthenticatedClient.CreateAsync(factory, "Instructor");
        Assert.Equal(HttpStatusCode.Forbidden, (await instructor.GetAsync($"/api/students/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task Administrator_CanAccessStudentEndpoint()
    {
        using var administrator = await AuthenticatedClient.CreateAsync(factory, "Administrator");
        Assert.Equal(HttpStatusCode.NotFound, (await administrator.GetAsync($"/api/students/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task BootstrapFailures_RollBackAndAllowRetry()
    {
        await AuthenticatedClient.ClearIdentityAsync(factory);
        var invalid = BootstrapConfiguration("short1");
        await Assert.ThrowsAsync<InvalidOperationException>(() => BootstrapAdministrator.InitializeAsync(factory.Services, invalid));
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TcmdDbContext>();
            Assert.False(db.Users.Any());
            Assert.False(db.Roles.Any());
        }
        await BootstrapAdministrator.InitializeAsync(factory.Services, BootstrapConfiguration("Password1"));
    }

    [Fact]
    public async Task BootstrapMissingConfigurationAndExistingUsers_DoNotWriteAnotherAccount()
    {
        await AuthenticatedClient.ClearIdentityAsync(factory);
        var incomplete = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["BootstrapAdmin:Enabled"] = "true" }).Build();
        await Assert.ThrowsAsync<InvalidOperationException>(() => BootstrapAdministrator.InitializeAsync(factory.Services, incomplete));
        await AuthenticatedClient.CreateAccountAsync(factory, "Staff", true);
        await Assert.ThrowsAsync<InvalidOperationException>(() => BootstrapAdministrator.InitializeAsync(factory.Services, BootstrapConfiguration("Password1")));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TcmdDbContext>();
        Assert.Single(db.Users);
        Assert.Single(db.Roles);
    }

    private static IConfiguration BootstrapConfiguration(string password) => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["BootstrapAdmin:Enabled"] = "true", ["BootstrapAdmin:UserName"] = "bootstrap", ["BootstrapAdmin:DisplayName"] = "Bootstrap", ["BootstrapAdmin:Password"] = password
    }).Build();
}
