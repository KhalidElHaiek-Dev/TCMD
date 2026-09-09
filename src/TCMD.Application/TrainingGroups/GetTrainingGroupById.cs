namespace TCMD.Application.TrainingGroups;

public sealed class GetTrainingGroupById(ITrainingGroupStore store)
{
    public async Task<TrainingGroupDto?> ExecuteAsync(Guid id, CancellationToken cancellationToken)
    {
        var group = await store.GetByIdAsync(id, cancellationToken);
        return group is null ? null : TrainingGroupDto.From(group);
    }
}
