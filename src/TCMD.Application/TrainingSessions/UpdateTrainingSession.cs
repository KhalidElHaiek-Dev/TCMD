using TCMD.Domain.TrainingSessions;

namespace TCMD.Application.TrainingSessions;

public sealed record UpdateTrainingSessionRequest(DateOnly SessionDate, TimeOnly StartTime,
    TimeOnly EndTime, string? Location, byte[] RowVersion);

public sealed class UpdateTrainingSession(ITrainingSessionStore store, TimeProvider timeProvider)
{
    public async Task<TrainingSessionMutationResult> ExecuteAsync(Guid id, UpdateTrainingSessionRequest request,
        CancellationToken cancellationToken)
    {
        var session = await store.GetForUpdateAsync(id, cancellationToken);
        if (session is null) return TrainingSessionMutationResult.Error(TrainingSessionMutationStatus.SessionNotFound);
        if (!TrainingSessionValidation.HasExpectedVersion(session, request.RowVersion))
            return TrainingSessionMutationResult.Error(TrainingSessionMutationStatus.ConcurrencyConflict);
        if (session.Status != TrainingSessionStatus.Scheduled)
            return TrainingSessionMutationResult.Error(TrainingSessionMutationStatus.TerminalSession);
        var group = await store.GetTrainingGroupReferenceAsync(session.TrainingGroupId, cancellationToken);
        if (!group.Exists) return TrainingSessionMutationResult.Error(TrainingSessionMutationStatus.TrainingGroupNotFound);
        if (!TrainingSessionValidation.IsEligible(group))
            return TrainingSessionMutationResult.Error(TrainingSessionMutationStatus.TrainingGroupIneligible);
        if (!TrainingSessionValidation.IsWithinRange(group, request.SessionDate))
            return TrainingSessionMutationResult.Error(TrainingSessionMutationStatus.SessionDateOutsideGroupRange);
        var changed = session.UpdateDetails(request.SessionDate, request.StartTime, request.EndTime,
            request.Location, timeProvider.GetUtcNow());
        return changed
            ? TrainingSessionMutationResult.FromStore(await store.SaveAsync(session, request.RowVersion, cancellationToken), session)
            : TrainingSessionMutationResult.Success(session);
    }
}
