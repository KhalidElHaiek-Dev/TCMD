using TCMD.Domain.TrainingSessions;

namespace TCMD.Application.TrainingSessions;

public sealed record CreateTrainingSessionRequest(DateOnly SessionDate, TimeOnly StartTime,
    TimeOnly EndTime, string? Location);

public sealed class CreateTrainingSession(ITrainingSessionStore store, TimeProvider timeProvider)
{
    public async Task<TrainingSessionMutationResult> ExecuteAsync(Guid groupId,
        CreateTrainingSessionRequest request, CancellationToken cancellationToken)
    {
        var group = await store.GetTrainingGroupReferenceAsync(groupId, cancellationToken);
        if (!group.Exists) return TrainingSessionMutationResult.Error(TrainingSessionMutationStatus.TrainingGroupNotFound);
        if (!TrainingSessionValidation.IsEligible(group))
            return TrainingSessionMutationResult.Error(TrainingSessionMutationStatus.TrainingGroupIneligible);
        if (!TrainingSessionValidation.IsWithinRange(group, request.SessionDate))
            return TrainingSessionMutationResult.Error(TrainingSessionMutationStatus.SessionDateOutsideGroupRange);
        var session = TrainingSession.Create(groupId, request.SessionDate, request.StartTime, request.EndTime,
            request.Location, timeProvider.GetUtcNow());
        return TrainingSessionMutationResult.FromStore(await store.AddAsync(session, cancellationToken), session);
    }
}
