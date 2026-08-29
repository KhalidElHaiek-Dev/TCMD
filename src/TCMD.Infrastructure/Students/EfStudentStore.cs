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
}
