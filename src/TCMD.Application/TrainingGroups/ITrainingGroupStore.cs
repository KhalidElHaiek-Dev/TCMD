using TCMD.Domain.TrainingGroups;

namespace TCMD.Application.TrainingGroups;

public enum TrainingGroupStoreSaveStatus { Success, ConcurrencyConflict, DuplicateGroup }
public sealed record TrainingGroupReference(bool Exists, bool IsActive);

public interface ITrainingGroupStore
{
    Task<TrainingGroupStoreSaveStatus> AddAsync(TrainingGroup group, CancellationToken cancellationToken);
    Task<TrainingGroup?> GetByIdAsync(Guid id, Guid? assignedInstructorId, CancellationToken cancellationToken);
    Task<TrainingGroup?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<TrainingGroup>> SearchAsync(string? search, TrainingGroupStatus? status, Guid? courseId,
        Guid? primaryInstructorId, bool? hasPrimaryInstructor, Guid? assignedInstructorId,
        CancellationToken cancellationToken);
    Task<TrainingGroupReference> GetCourseReferenceAsync(Guid id, CancellationToken cancellationToken);
    Task<TrainingGroupReference> GetInstructorReferenceAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> DuplicateExistsAsync(Guid courseId, string name, DateOnly plannedStartDate, Guid? excludingId,
        CancellationToken cancellationToken);
    Task<bool> HasNonCancelledSessionsOutsideRangeAsync(Guid groupId, DateOnly startDate, DateOnly endDate,
        CancellationToken cancellationToken);
    Task<TrainingGroupStoreSaveStatus> SaveAsync(TrainingGroup group, byte[] expectedRowVersion,
        CancellationToken cancellationToken);
}
