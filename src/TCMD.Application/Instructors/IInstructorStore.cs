using TCMD.Domain.Instructors;

namespace TCMD.Application.Instructors;

public interface IInstructorStore
{
    Task AddAsync(Instructor instructor, CancellationToken cancellationToken);
    Task<Instructor?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<Instructor?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<Instructor>> SearchAsync(string? search, bool? isActive, CancellationToken cancellationToken);
    Task<bool> SaveAsync(Instructor instructor, byte[] expectedRowVersion, CancellationToken cancellationToken);
}
