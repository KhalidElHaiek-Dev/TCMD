namespace TCMD.Application.TrainingGroups;

public sealed record CreateTrainingGroupRequest(string Name, Guid CourseId, Guid? PrimaryInstructorId,
    DateOnly PlannedStartDate, DateOnly PlannedEndDate);

public sealed class CreateTrainingGroup(ITrainingGroupStore store, TimeProvider timeProvider)
{
    public async Task<TrainingGroupMutationResult> ExecuteAsync(CreateTrainingGroupRequest request, CancellationToken cancellationToken)
    {
        var error = await TrainingGroupValidation.ValidateCourseAsync(store, request.CourseId, cancellationToken)
            ?? await TrainingGroupValidation.ValidateInstructorAsync(store, request.PrimaryInstructorId, cancellationToken);
        if (error is not null) return TrainingGroupMutationResult.Error(error.Value);
        var group = Domain.TrainingGroups.TrainingGroup.Create(request.Name, request.CourseId, request.PrimaryInstructorId,
            request.PlannedStartDate, request.PlannedEndDate, timeProvider.GetUtcNow());
        if (await store.DuplicateExistsAsync(group.CourseId, group.Name, group.PlannedStartDate, null, cancellationToken))
            return TrainingGroupMutationResult.Error(TrainingGroupMutationStatus.DuplicateGroup);
        return TrainingGroupMutationResult.FromStore(await store.AddAsync(group, cancellationToken), group);
    }
}
