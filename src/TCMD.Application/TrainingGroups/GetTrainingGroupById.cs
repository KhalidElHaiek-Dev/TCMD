namespace TCMD.Application.TrainingGroups;

public sealed class GetTrainingGroupById(ITrainingGroupStore store)
{
    public async Task<TrainingGroupDto?> ExecuteAsync(Guid id, Guid? assignedInstructorId,
        CancellationToken cancellationToken)
    {
        var group = await store.GetByIdAsync(id, assignedInstructorId, cancellationToken);
        return group is null ? null : TrainingGroupDto.From(group);
    }
}
