using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TCMD.Infrastructure.Identity;
using TCMD.Infrastructure.Persistence;

namespace TCMD.Api.Authentication;

public static class BootstrapAdministrator
{
    public static async Task InitializeAsync(IServiceProvider services, IConfiguration configuration)
    {
        if (!configuration.GetValue<bool>("BootstrapAdmin:Enabled")) return;
        var userName = configuration["BootstrapAdmin:UserName"];
        var displayName = configuration["BootstrapAdmin:DisplayName"];
        var password = configuration["BootstrapAdmin:Password"];
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(displayName) || string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("Bootstrap administrator configuration is incomplete.");

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TcmdDbContext>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<StaffUser>>();
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            if (await db.Users.AnyAsync())
                throw new InvalidOperationException("Bootstrap administrator cannot run when staff accounts exist.");

            foreach (var role in TcmdPolicies.Roles)
            {
                if (!await roles.RoleExistsAsync(role) && !(await roles.CreateAsync(new IdentityRole<Guid>(role))).Succeeded)
                    throw new InvalidOperationException("Bootstrap role creation failed.");
            }

            var admin = new StaffUser { UserName = userName.Trim(), DisplayName = displayName.Trim(), IsActive = true };
            if (!(await users.CreateAsync(admin, password)).Succeeded)
                throw new InvalidOperationException("Bootstrap administrator could not be created.");
            if (!(await users.AddToRoleAsync(admin, "Administrator")).Succeeded)
                throw new InvalidOperationException("Bootstrap administrator role assignment failed.");

            await transaction.CommitAsync();
        });
    }
}
