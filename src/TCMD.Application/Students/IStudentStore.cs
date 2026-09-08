using TCMD.Domain.Students;

namespace TCMD.Application.Students;

public interface IStudentStore
{
    Task AddAsync(Student student, CancellationToken cancellationToken);
    Task<Student?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<Student?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<Student>> SearchAsync(string? search, bool? isActive, CancellationToken cancellationToken);
    Task<bool> SaveAsync(Student student, byte[] expectedRowVersion, CancellationToken cancellationToken);
}
