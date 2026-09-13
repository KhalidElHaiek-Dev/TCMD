namespace TCMD.Application.TrainingSessions;

public sealed record TrainingSessionListResult(TrainingSessionMutationStatus Status,
    IReadOnlyList<TrainingSessionDto>? Sessions);

public sealed class ListTrainingGroupSessions(ITrainingSessionStore store)
{
    public async Task<TrainingSessionListResult> ExecuteAsync(Guid groupId, Guid? assignedInstructorId,
        CancellationToken cancellationToken)
    {
        if (assignedInstructorId is not null && !await store.IsTrainingGroupAssignedAsync(groupId,
                assignedInstructorId.Value, cancellationToken))
            return new(TrainingSessionMutationStatus.TrainingGroupNotFound, null);
        var group = await store.GetTrainingGroupReferenceAsync(groupId, cancellationToken);
        if (!group.Exists) return new(TrainingSessionMutationStatus.TrainingGroupNotFound, null);
        var sessions = await store.ListByTrainingGroupAsync(groupId, assignedInstructorId, cancellationToken);
        return new(TrainingSessionMutationStatus.Success, sessions.Select(TrainingSessionDto.From).ToArray());
    }
}
