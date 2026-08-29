using TCMD.Domain.Students;

namespace TCMD.Application.Students;

public interface IStudentStore
{
    Task AddAsync(Student student, CancellationToken cancellationToken);
    Task<Student?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}
