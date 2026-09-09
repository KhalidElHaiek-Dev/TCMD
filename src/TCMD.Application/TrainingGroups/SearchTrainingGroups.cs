using TCMD.Domain.TrainingGroups;

namespace TCMD.Application.TrainingGroups;

public sealed class SearchTrainingGroups(ITrainingGroupStore store)
{
    public async Task<IReadOnlyList<TrainingGroupDto>> ExecuteAsync(string? search, TrainingGroupStatus? status,
        Guid? courseId, Guid? primaryInstructorId, bool? hasPrimaryInstructor, CancellationToken cancellationToken)
    {
        var groups = await store.SearchAsync(string.IsNullOrWhiteSpace(search) ? null : search.Trim(), status,
            courseId, primaryInstructorId, hasPrimaryInstructor, cancellationToken);
        return groups.Select(TrainingGroupDto.From).ToArray();
    }
}
