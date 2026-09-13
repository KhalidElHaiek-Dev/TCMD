namespace TCMD.Application.TrainingSessions;

public sealed class GetTrainingSessionById(ITrainingSessionStore store)
{
    public async Task<TrainingSessionDto?> ExecuteAsync(Guid id, Guid? assignedInstructorId,
        CancellationToken cancellationToken)
    {
        var session = assignedInstructorId is null
            ? await store.GetByIdAsync(id, cancellationToken)
            : await store.GetAssignedByIdAsync(id, assignedInstructorId.Value, cancellationToken);
        return session is null ? null : TrainingSessionDto.From(session);
    }
}
