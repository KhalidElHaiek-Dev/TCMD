namespace TCMD.Application.StaffAccounts;

public static class StaffRoles
{
    public const string Administrator = "Administrator";
    public const string Staff = "Staff";
    public const string Instructor = "Instructor";
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Administrator, Staff, Instructor
    };
}

public sealed record StaffAccountDto(Guid Id, string UserName, string DisplayName, string Role, bool IsActive,
    Guid? InstructorId);

public sealed record CreateStaffAccountRequest(string UserName, string DisplayName, string Password, string Role);

public enum StaffAccountError
{
    None,
    NotFound,
    DuplicateUserName,
    InvalidUserName,
    InvalidPassword,
    InvalidRole,
    CannotDeactivateSelf,
    LastActiveAdministrator,
    InstructorNotFound,
    AccountNotInstructor,
    InstructorInactive,
    InstructorAlreadyLinked,
    ConcurrencyConflict
}

public sealed record StaffAccountResult(StaffAccountDto? Account, StaffAccountError Error)
{
    public bool Succeeded => Error == StaffAccountError.None;
    public static StaffAccountResult Success(StaffAccountDto account) => new(account, StaffAccountError.None);
    public static StaffAccountResult Failure(StaffAccountError error) => new(null, error);
}

public interface IStaffAccountStore
{
    Task<IReadOnlyList<StaffAccountDto>> ListAsync(CancellationToken cancellationToken);
    Task<StaffAccountDto?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<StaffAccountResult> CreateAsync(CreateStaffAccountRequest request, CancellationToken cancellationToken);
    Task<StaffAccountResult> SetActiveAsync(Guid id, bool isActive, Guid currentUserId, CancellationToken cancellationToken);
    Task<StaffAccountResult> ChangeRoleAsync(Guid id, string role, CancellationToken cancellationToken);
    Task<StaffAccountResult> ReplacePasswordAsync(Guid id, string newPassword, CancellationToken cancellationToken);
    Task<StaffAccountResult> SetInstructorLinkAsync(Guid id, Guid? instructorId,
        CancellationToken cancellationToken);
}

public sealed class StaffAccountAdministration(IStaffAccountStore store)
{
    public Task<IReadOnlyList<StaffAccountDto>> ListAsync(CancellationToken cancellationToken) =>
        store.ListAsync(cancellationToken);

    public Task<StaffAccountDto?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        store.GetAsync(id, cancellationToken);

    public Task<StaffAccountResult> CreateAsync(CreateStaffAccountRequest request, CancellationToken cancellationToken) =>
        store.CreateAsync(request, cancellationToken);

    public Task<StaffAccountResult> SetActiveAsync(Guid id, bool isActive, Guid currentUserId, CancellationToken cancellationToken) =>
        store.SetActiveAsync(id, isActive, currentUserId, cancellationToken);

    public Task<StaffAccountResult> ChangeRoleAsync(Guid id, string role, CancellationToken cancellationToken) =>
        store.ChangeRoleAsync(id, role, cancellationToken);

    public Task<StaffAccountResult> ReplacePasswordAsync(Guid id, string newPassword, CancellationToken cancellationToken) =>
        store.ReplacePasswordAsync(id, newPassword, cancellationToken);

    public Task<StaffAccountResult> SetInstructorLinkAsync(Guid id, Guid? instructorId,
        CancellationToken cancellationToken) => store.SetInstructorLinkAsync(id, instructorId, cancellationToken);
}
