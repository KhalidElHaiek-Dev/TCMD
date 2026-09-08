using Microsoft.EntityFrameworkCore;
using TCMD.Application.Students;
using TCMD.Domain.Students;
using TCMD.Infrastructure.Persistence;

namespace TCMD.Infrastructure.Students;

internal sealed class EfStudentStore(TcmdDbContext dbContext) : IStudentStore
{
    public async Task AddAsync(Student student, CancellationToken cancellationToken)
    {
        dbContext.Students.Add(student);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<Student?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Students.AsNoTracking().SingleOrDefaultAsync(student => student.Id == id, cancellationToken);

    public Task<Student?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Students.SingleOrDefaultAsync(student => student.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Student>> SearchAsync(
        string? search,
        bool? isActive,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Students.AsNoTracking();
        if (isActive is not null)
        {
            query = query.Where(student => student.IsActive == isActive);
        }

        if (search is not null)
        {
            query = query.Where(student =>
                student.StudentNumber.Contains(search) ||
                student.FullName.Contains(search) ||
                student.PhoneNumber.Contains(search) ||
                (student.Email != null && student.Email.Contains(search)));
        }

        return await query.OrderBy(student => student.StudentNumber).ToListAsync(cancellationToken);
    }

    public async Task<bool> SaveAsync(
        Student student,
        byte[] expectedRowVersion,
        CancellationToken cancellationToken)
    {
        dbContext.Entry(student).Property(candidate => candidate.RowVersion).OriginalValue = expectedRowVersion;
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.Entry(student).State = EntityState.Detached;
            return false;
        }
    }
}
