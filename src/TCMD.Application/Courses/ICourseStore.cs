using TCMD.Domain.Courses;

namespace TCMD.Application.Courses;

public enum CourseStoreSaveStatus { Success, ConcurrencyConflict, DuplicateCode }

public interface ICourseStore
{
    Task<CourseStoreSaveStatus> AddAsync(Course course, CancellationToken cancellationToken);
    Task<Course?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<Course?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<Course>> SearchAsync(string? search, bool? isActive, CancellationToken cancellationToken);
    Task<CourseStoreSaveStatus> SaveAsync(Course course, byte[] expectedRowVersion, CancellationToken cancellationToken);
}
