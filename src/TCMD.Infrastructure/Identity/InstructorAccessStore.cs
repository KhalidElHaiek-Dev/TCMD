using Microsoft.EntityFrameworkCore;
using TCMD.Application.StaffAccounts;
using TCMD.Infrastructure.Persistence;

namespace TCMD.Infrastructure.Identity;

internal sealed class InstructorAccessStore(TcmdDbContext db) : IInstructorAccessStore
{
    public async Task<InstructorIdentity> ResolveAsync(Guid staffUserId, CancellationToken cancellationToken)
    {
        var row = await (from user in db.Users.AsNoTracking()
            join instructor in db.Instructors.AsNoTracking() on user.InstructorId equals instructor.Id into instructors
            from instructor in instructors.DefaultIfEmpty()
            where user.Id == staffUserId
            select new { user.InstructorId, IsActive = instructor != null && instructor.IsActive })
            .SingleAsync(cancellationToken);
        return row.InstructorId is null ? new(InstructorIdentityStatus.Unlinked, null)
            : row.IsActive ? new(InstructorIdentityStatus.Active, row.InstructorId)
            : new(InstructorIdentityStatus.Inactive, row.InstructorId);
    }
}
