using TCMD.Domain.TrainingSessions;

namespace TCMD.Application.TrainingSessions;

public abstract class ChangeTrainingSessionStatus(ITrainingSessionStore store, TimeProvider timeProvider)
{
    protected async Task<TrainingSessionMutationResult> ExecuteAsync(Guid id, byte[] rowVersion,
        Func<TrainingSession, DateTimeOffset, bool> change, TrainingSessionStatus sameStatus,
        CancellationToken cancellationToken)
    {
        var session = await store.GetForUpdateAsync(id, cancellationToken);
        if (session is null) return TrainingSessionMutationResult.Error(TrainingSessionMutationStatus.SessionNotFound);
        if (!TrainingSessionValidation.HasExpectedVersion(session, rowVersion))
            return TrainingSessionMutationResult.Error(TrainingSessionMutationStatus.ConcurrencyConflict);
        if (session.Status != TrainingSessionStatus.Scheduled && session.Status != sameStatus)
            return TrainingSessionMutationResult.Error(TrainingSessionMutationStatus.InvalidStatusTransition);
        var changed = change(session, timeProvider.GetUtcNow());
        return changed
            ? TrainingSessionMutationResult.FromStore(await store.SaveAsync(session, rowVersion, cancellationToken), session)
            : TrainingSessionMutationResult.Success(session);
    }
}

public sealed class CompleteTrainingSession(ITrainingSessionStore store, TimeProvider timeProvider)
    : ChangeTrainingSessionStatus(store, timeProvider)
{
    public Task<TrainingSessionMutationResult> ExecuteAsync(Guid id, byte[] rowVersion,
        CancellationToken cancellationToken) => ExecuteAsync(id, rowVersion,
        static (session, now) => session.Complete(now), TrainingSessionStatus.Completed, cancellationToken);
}

public sealed class CancelTrainingSession(ITrainingSessionStore store, TimeProvider timeProvider)
    : ChangeTrainingSessionStatus(store, timeProvider)
{
    public Task<TrainingSessionMutationResult> ExecuteAsync(Guid id, byte[] rowVersion,
        CancellationToken cancellationToken) => ExecuteAsync(id, rowVersion,
        static (session, now) => session.Cancel(now), TrainingSessionStatus.Cancelled, cancellationToken);
}
