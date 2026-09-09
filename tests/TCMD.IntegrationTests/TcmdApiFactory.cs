using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TCMD.Infrastructure.Persistence;

namespace TCMD.IntegrationTests;

public sealed class TcmdApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly TestTimeProvider testTimeProvider = new();
    private const string LocalDbFallbackConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Database=TCMD.IntegrationTests;Trusted_Connection=True;TrustServerCertificate=True";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var connectionString = GetTestConnectionString();
        EnsureDedicatedTestDatabase(connectionString);
        builder.UseSetting("ConnectionStrings:TCMD", connectionString);
        builder.ConfigureServices(services => services.AddSingleton<TimeProvider>(testTimeProvider));
    }

    public async Task InitializeAsync()
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TcmdDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    public void SetUtcNow(DateTimeOffset value) => testTimeProvider.SetUtcNow(value);
    public void ResetUtcNow() => testTimeProvider.Reset();

    private static void EnsureDedicatedTestDatabase(string connectionString)
    {
        var databaseName = new SqlConnectionStringBuilder(connectionString).InitialCatalog;
        if (!string.Equals(databaseName, "TCMD.IntegrationTests", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Integration tests must use the dedicated TCMD.IntegrationTests database.");
        }
    }

    private static string GetTestConnectionString()
    {
        var environmentConnectionString = Environment.GetEnvironmentVariable("TCMD_TEST_CONNECTION_STRING");
        if (!string.IsNullOrWhiteSpace(environmentConnectionString))
        {
            return environmentConnectionString;
        }

        var testConfiguration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.IntegrationTests.json", optional: true)
            .Build();

        var configuredConnectionString = testConfiguration.GetConnectionString("TCMD");
        return string.IsNullOrWhiteSpace(configuredConnectionString)
            ? LocalDbFallbackConnectionString
            : configuredConnectionString;
    }
}

internal sealed class TestTimeProvider : TimeProvider
{
    private DateTimeOffset? overriddenUtcNow;
    public override DateTimeOffset GetUtcNow() => overriddenUtcNow ?? DateTimeOffset.UtcNow;
    public void SetUtcNow(DateTimeOffset value) => overriddenUtcNow = value;
    public void Reset() => overriddenUtcNow = null;
}
