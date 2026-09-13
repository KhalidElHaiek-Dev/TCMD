using TCMD.Domain.TrainingGroups;
using TCMD.Domain.TrainingSessions;

namespace TCMD.Application.TrainingSessions;

public enum TrainingSessionStoreSaveStatus { Success, ConcurrencyConflict }
public sealed record TrainingSessionGroupReference(bool Exists, TrainingGroupStatus? Status,
    DateOnly PlannedStartDate, DateOnly PlannedEndDate);

public interface ITrainingSessionStore
{
    Task<TrainingSessionGroupReference> GetTrainingGroupReferenceAsync(Guid id, CancellationToken cancellationToken);
    Task<TrainingSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<TrainingSession?> GetAssignedByIdAsync(Guid id, Guid instructorId, CancellationToken cancellationToken);
    Task<TrainingSession?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<TrainingSession>> ListByTrainingGroupAsync(Guid groupId, Guid? assignedInstructorId,
        CancellationToken cancellationToken);
    Task<bool> IsTrainingGroupAssignedAsync(Guid groupId, Guid instructorId, CancellationToken cancellationToken);
    Task<TrainingSessionStoreSaveStatus> AddAsync(TrainingSession session, CancellationToken cancellationToken);
    Task<TrainingSessionStoreSaveStatus> SaveAsync(TrainingSession session, byte[] expectedRowVersion,
        CancellationToken cancellationToken);
}
