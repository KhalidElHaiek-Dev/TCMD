namespace TCMD.Application.StaffAccounts;

public enum InstructorIdentityStatus { Active, Unlinked, Inactive }

public sealed record InstructorIdentity(InstructorIdentityStatus Status, Guid? InstructorId);

public interface IInstructorAccessStore
{
    Task<InstructorIdentity> ResolveAsync(Guid staffUserId, CancellationToken cancellationToken);
}
