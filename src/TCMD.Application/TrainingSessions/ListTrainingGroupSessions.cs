namespace TCMD.Application.TrainingSessions;

public sealed record TrainingSessionListResult(TrainingSessionMutationStatus Status,
    IReadOnlyList<TrainingSessionDto>? Sessions);

public sealed class ListTrainingGroupSessions(ITrainingSessionStore store)
{
    public async Task<TrainingSessionListResult> ExecuteAsync(Guid groupId, CancellationToken cancellationToken)
    {
        var group = await store.GetTrainingGroupReferenceAsync(groupId, cancellationToken);
        if (!group.Exists) return new(TrainingSessionMutationStatus.TrainingGroupNotFound, null);
        var sessions = await store.ListByTrainingGroupAsync(groupId, cancellationToken);
        return new(TrainingSessionMutationStatus.Success, sessions.Select(TrainingSessionDto.From).ToArray());
    }
}
