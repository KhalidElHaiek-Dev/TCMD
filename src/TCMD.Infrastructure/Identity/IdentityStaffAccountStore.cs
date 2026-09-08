using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TCMD.Application.StaffAccounts;
using TCMD.Infrastructure.Persistence;

namespace TCMD.Infrastructure.Identity;

internal sealed class IdentityStaffAccountStore(
    TcmdDbContext dbContext,
    UserManager<StaffUser> userManager) : IStaffAccountStore
{
    public async Task<IReadOnlyList<StaffAccountDto>> ListAsync(CancellationToken cancellationToken)
    {
        var users = await userManager.Users.OrderBy(user => user.UserName).ToListAsync(cancellationToken);
        var accounts = new List<StaffAccountDto>(users.Count);
        foreach (var user in users) accounts.Add(await ToDtoAsync(user));
        return accounts;
    }

    public async Task<StaffAccountDto?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await userManager.Users.SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        return user is null ? null : await ToDtoAsync(user);
    }

    public Task<StaffAccountResult> CreateAsync(CreateStaffAccountRequest request, CancellationToken cancellationToken)
    {
        if (!StaffRoles.All.Contains(request.Role)) return Task.FromResult(StaffAccountResult.Failure(StaffAccountError.InvalidRole));
        return ExecuteTransactionAsync(async () =>
        {
            var user = new StaffUser { UserName = request.UserName.Trim(), DisplayName = request.DisplayName.Trim(), IsActive = true };
            var created = await userManager.CreateAsync(user, request.Password);
            if (!created.Succeeded)
            {
                if (created.Errors.Any(error => error.Code == "DuplicateUserName"))
                    return StaffAccountResult.Failure(StaffAccountError.DuplicateUserName);
                if (created.Errors.Any(error => error.Code == "InvalidUserName"))
                    return StaffAccountResult.Failure(StaffAccountError.InvalidUserName);
                if (HasPasswordError(created))
                    return StaffAccountResult.Failure(StaffAccountError.InvalidPassword);
                throw new InvalidOperationException("Staff account could not be created.");
            }
            if (!(await userManager.AddToRoleAsync(user, request.Role)).Succeeded)
                throw new InvalidOperationException("Staff account role could not be assigned.");
            return StaffAccountResult.Success(await ToDtoAsync(user));
        });
    }

    public Task<StaffAccountResult> SetActiveAsync(Guid id, bool isActive, Guid currentUserId, CancellationToken cancellationToken) =>
        ExecuteTransactionAsync(async () =>
        {
            var user = await userManager.FindByIdAsync(id.ToString());
            if (user is null) return StaffAccountResult.Failure(StaffAccountError.NotFound);
            if (!isActive && id == currentUserId) return StaffAccountResult.Failure(StaffAccountError.CannotDeactivateSelf);
            if (!isActive && user.IsActive && await userManager.IsInRoleAsync(user, StaffRoles.Administrator)
                && await CountActiveAdministratorsAsync(cancellationToken) <= 1)
                return StaffAccountResult.Failure(StaffAccountError.LastActiveAdministrator);
            user.IsActive = isActive;
            if (!(await userManager.UpdateSecurityStampAsync(user)).Succeeded)
                throw new InvalidOperationException("Staff account active status could not be changed.");
            return StaffAccountResult.Success(await ToDtoAsync(user));
        });

    public Task<StaffAccountResult> ChangeRoleAsync(Guid id, string role, CancellationToken cancellationToken)
    {
        if (!StaffRoles.All.Contains(role)) return Task.FromResult(StaffAccountResult.Failure(StaffAccountError.InvalidRole));
        return ExecuteTransactionAsync(async () =>
        {
            var user = await userManager.FindByIdAsync(id.ToString());
            if (user is null) return StaffAccountResult.Failure(StaffAccountError.NotFound);
            var currentRoles = await userManager.GetRolesAsync(user);
            if (user.IsActive && currentRoles.Contains(StaffRoles.Administrator) && role != StaffRoles.Administrator
                && await CountActiveAdministratorsAsync(cancellationToken) <= 1)
                return StaffAccountResult.Failure(StaffAccountError.LastActiveAdministrator);
            if (currentRoles.Count > 0 && !(await userManager.RemoveFromRolesAsync(user, currentRoles)).Succeeded)
                throw new InvalidOperationException("Staff account role could not be changed.");
            if (!(await userManager.AddToRoleAsync(user, role)).Succeeded)
                throw new InvalidOperationException("Staff account role could not be changed.");
            if (!(await userManager.UpdateSecurityStampAsync(user)).Succeeded)
                throw new InvalidOperationException("Staff account role could not be changed.");
            return StaffAccountResult.Success(await ToDtoAsync(user));
        });
    }

    public async Task<StaffAccountResult> ReplacePasswordAsync(Guid id, string newPassword, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null) return StaffAccountResult.Failure(StaffAccountError.NotFound);
        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var reset = await userManager.ResetPasswordAsync(user, token, newPassword);
        if (reset.Succeeded) return StaffAccountResult.Success(await ToDtoAsync(user));
        if (HasPasswordError(reset)) return StaffAccountResult.Failure(StaffAccountError.InvalidPassword);
        throw new InvalidOperationException("Staff account password could not be replaced.");
    }

    private async Task<StaffAccountDto> ToDtoAsync(StaffUser user)
    {
        var roles = await userManager.GetRolesAsync(user);
        return new StaffAccountDto(user.Id, user.UserName!, user.DisplayName, roles.Single(), user.IsActive);
    }

    private Task<int> CountActiveAdministratorsAsync(CancellationToken cancellationToken) =>
        (from user in dbContext.Users
         join userRole in dbContext.UserRoles on user.Id equals userRole.UserId
         join role in dbContext.Roles on userRole.RoleId equals role.Id
         where user.IsActive && role.Name == StaffRoles.Administrator
         select user.Id).CountAsync(cancellationToken);

    private static bool HasPasswordError(IdentityResult result) =>
        result.Errors.Any(error => error.Code.StartsWith("Password", StringComparison.Ordinal));

    private async Task<StaffAccountResult> ExecuteTransactionAsync(Func<Task<StaffAccountResult>> operation)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var result = await operation();
            if (result.Succeeded) await transaction.CommitAsync();
            return result;
        });
    }
}
