using TCMD.Domain.TrainingGroups;

namespace TCMD.Application.TrainingGroups;

public abstract class ChangeTrainingGroupStatus(ITrainingGroupStore store, TimeProvider timeProvider)
{
    protected async Task<TrainingGroupMutationResult> ChangeAsync(Guid id, byte[] rowVersion,
        TrainingGroupStatus target, CancellationToken cancellationToken)
    {
        var group = await store.GetForUpdateAsync(id, cancellationToken);
        if (group is null) return TrainingGroupMutationResult.Error(TrainingGroupMutationStatus.NotFound);
        if (!TrainingGroupValidation.HasExpectedVersion(group, rowVersion))
            return TrainingGroupMutationResult.Error(TrainingGroupMutationStatus.ConcurrencyConflict);
        if (target == TrainingGroupStatus.Active)
        {
            if (group.PrimaryInstructorId is null)
                return TrainingGroupMutationResult.Error(TrainingGroupMutationStatus.InstructorRequired);
            var referenceError = await TrainingGroupValidation.ValidateCourseAsync(store, group.CourseId, cancellationToken)
                ?? await TrainingGroupValidation.ValidateInstructorAsync(store, group.PrimaryInstructorId, cancellationToken);
            if (referenceError is not null) return TrainingGroupMutationResult.Error(referenceError.Value);
        }

        bool changed;
        try
        {
            var now = timeProvider.GetUtcNow();
            changed = target switch
            {
                TrainingGroupStatus.Active => group.Activate(now),
                TrainingGroupStatus.Completed => group.Complete(now),
                TrainingGroupStatus.Cancelled => group.Cancel(now),
                _ => throw new ArgumentOutOfRangeException(nameof(target))
            };
        }
        catch (InvalidOperationException)
        {
            return TrainingGroupMutationResult.Error(TrainingGroupMutationStatus.InvalidStatusTransition);
        }
        return changed
            ? TrainingGroupMutationResult.FromStore(await store.SaveAsync(group, rowVersion, cancellationToken), group)
            : TrainingGroupMutationResult.Success(group);
    }
}

public sealed class ActivateTrainingGroup(ITrainingGroupStore store, TimeProvider timeProvider)
    : ChangeTrainingGroupStatus(store, timeProvider)
{
    public Task<TrainingGroupMutationResult> ExecuteAsync(Guid id, byte[] rowVersion, CancellationToken cancellationToken) =>
        ChangeAsync(id, rowVersion, TrainingGroupStatus.Active, cancellationToken);
}

public sealed class CompleteTrainingGroup(ITrainingGroupStore store, TimeProvider timeProvider)
    : ChangeTrainingGroupStatus(store, timeProvider)
{
    public Task<TrainingGroupMutationResult> ExecuteAsync(Guid id, byte[] rowVersion, CancellationToken cancellationToken) =>
        ChangeAsync(id, rowVersion, TrainingGroupStatus.Completed, cancellationToken);
}

public sealed class CancelTrainingGroup(ITrainingGroupStore store, TimeProvider timeProvider)
    : ChangeTrainingGroupStatus(store, timeProvider)
{
    public Task<TrainingGroupMutationResult> ExecuteAsync(Guid id, byte[] rowVersion, CancellationToken cancellationToken) =>
        ChangeAsync(id, rowVersion, TrainingGroupStatus.Cancelled, cancellationToken);
}
