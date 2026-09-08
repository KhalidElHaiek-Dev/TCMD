using Microsoft.EntityFrameworkCore;
using TCMD.Application.Instructors;
using TCMD.Domain.Instructors;
using TCMD.Infrastructure.Persistence;

namespace TCMD.Infrastructure.Instructors;

internal sealed class EfInstructorStore(TcmdDbContext dbContext) : IInstructorStore
{
    public async Task AddAsync(Instructor instructor, CancellationToken cancellationToken)
    {
        dbContext.Instructors.Add(instructor);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<Instructor?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Instructors.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<Instructor?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Instructors.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Instructor>> SearchAsync(string? search, bool? isActive, CancellationToken cancellationToken)
    {
        var query = dbContext.Instructors.AsNoTracking();
        if (isActive is not null) query = query.Where(x => x.IsActive == isActive);
        if (search is not null)
            query = query.Where(x => x.FullName.Contains(search) ||
                (x.PhoneNumber != null && x.PhoneNumber.Contains(search)) ||
                (x.Email != null && x.Email.Contains(search)));
        return await query.OrderBy(x => x.FullName).ThenBy(x => x.Id).ToListAsync(cancellationToken);
    }

    public async Task<bool> SaveAsync(Instructor instructor, byte[] expectedRowVersion, CancellationToken cancellationToken)
    {
        dbContext.Entry(instructor).Property(x => x.RowVersion).OriginalValue = expectedRowVersion;
        try { await dbContext.SaveChangesAsync(cancellationToken); return true; }
        catch (DbUpdateConcurrencyException) { dbContext.Entry(instructor).State = EntityState.Detached; return false; }
    }
}
