using Microsoft.EntityFrameworkCore;
using TCMD.Infrastructure.Persistence;

namespace TCMD.Api.Development;

public static class DemoSeedCommand
{
    public static async Task RunAsync(IServiceProvider services, IHostEnvironment environment,
        CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TcmdDbContext>();
        DemoSeedSafety.EnsureAllowed(environment.EnvironmentName, db.Database.GetDbConnection().Database);

        Console.WriteLine("DEVELOPMENT DEMO SEED: targeting database 'TCMD.Demo'. Existing data will not be deleted or overwritten.");
        await db.Database.MigrateAsync(cancellationToken);
        await scope.ServiceProvider.GetRequiredService<DemoDataSeeder>().SeedAsync(cancellationToken);
        Console.WriteLine("Development demo data created successfully. The application was not started.");
    }
}

public static class DemoSeedSafety
{
    public static void EnsureAllowed(string environmentName, string databaseName)
    {
        if (!string.Equals(environmentName, Environments.Development, StringComparison.Ordinal))
            throw new InvalidOperationException("Demo seeding is allowed only in the Development environment.");
        if (!string.Equals(databaseName, "TCMD.Demo", StringComparison.Ordinal))
            throw new InvalidOperationException("Demo seeding requires the explicitly designated 'TCMD.Demo' database.");
    }
}
