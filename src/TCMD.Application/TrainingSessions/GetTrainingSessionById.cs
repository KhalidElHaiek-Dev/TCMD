namespace TCMD.Application.TrainingSessions;

public sealed class GetTrainingSessionById(ITrainingSessionStore store)
{
    public async Task<TrainingSessionDto?> ExecuteAsync(Guid id, CancellationToken cancellationToken)
    {
        var session = await store.GetByIdAsync(id, cancellationToken);
        return session is null ? null : TrainingSessionDto.From(session);
    }
}
