using System.Security.Claims;
using TCMD.Application.StaffAccounts;

namespace TCMD.Api.Authentication;

public sealed record RequestAccess(Guid StaffUserId, Guid? InstructorId, bool IsInstructor)
{
    public static RequestAccess Operational(Guid id) => new(id, null, false);
    public static RequestAccess Instructor(Guid id, Guid instructorId) => new(id, instructorId, true);
}

public sealed class RequestAccessResolver(IInstructorAccessStore instructorAccess)
{
    public async Task<RequestAccess?> ResolveAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var staffUserId)) return null;
        if (!principal.IsInRole(StaffRoles.Instructor)) return RequestAccess.Operational(staffUserId);
        var identity = await instructorAccess.ResolveAsync(staffUserId, cancellationToken);
        return identity.Status == InstructorIdentityStatus.Active
            ? RequestAccess.Instructor(staffUserId, identity.InstructorId!.Value) : null;
    }
}
