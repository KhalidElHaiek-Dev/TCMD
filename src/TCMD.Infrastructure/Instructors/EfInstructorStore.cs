using Microsoft.EntityFrameworkCore;
using TCMD.Application.Instructors;
using TCMD.Domain.Instructors;
using TCMD.Infrastructure.Persistence;
using System.Data;

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
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.RepeatableRead, cancellationToken);
            await dbContext.Instructors.AsNoTracking().Where(candidate => candidate.Id == instructor.Id)
                .Select(candidate => candidate.IsActive).SingleAsync(cancellationToken);
            dbContext.Entry(instructor).Property(x => x.RowVersion).OriginalValue = expectedRowVersion;
            var linkedUser = await dbContext.Users.SingleOrDefaultAsync(user => user.InstructorId == instructor.Id,
                cancellationToken);
            if (!instructor.IsActive && linkedUser is not null)
                linkedUser.SecurityStamp = Guid.NewGuid().ToString();
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return true;
            }
            catch (DbUpdateConcurrencyException)
            {
                dbContext.Entry(instructor).State = EntityState.Detached;
                return false;
            }
        });
    }
}
