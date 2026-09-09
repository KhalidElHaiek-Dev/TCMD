using TCMD.Domain.TrainingGroups;

namespace TCMD.Application.TrainingGroups;

public sealed record UpdateTrainingGroupRequest(string Name, Guid CourseId, Guid? PrimaryInstructorId,
    DateOnly PlannedStartDate, DateOnly PlannedEndDate, byte[] RowVersion);

public sealed class UpdateTrainingGroup(ITrainingGroupStore store, TimeProvider timeProvider)
{
    public async Task<TrainingGroupMutationResult> ExecuteAsync(Guid id, UpdateTrainingGroupRequest request,
        CancellationToken cancellationToken)
    {
        var group = await store.GetForUpdateAsync(id, cancellationToken);
        if (group is null) return TrainingGroupMutationResult.Error(TrainingGroupMutationStatus.NotFound);
        if (!TrainingGroupValidation.HasExpectedVersion(group, request.RowVersion))
            return TrainingGroupMutationResult.Error(TrainingGroupMutationStatus.ConcurrencyConflict);
        if (group.Status is TrainingGroupStatus.Completed or TrainingGroupStatus.Cancelled)
            return TrainingGroupMutationResult.Error(TrainingGroupMutationStatus.ProhibitedChange);
        if (group.Status == TrainingGroupStatus.Active &&
            (group.CourseId != request.CourseId || request.PrimaryInstructorId is null))
            return TrainingGroupMutationResult.Error(TrainingGroupMutationStatus.ProhibitedChange);

        TrainingGroupMutationStatus? error = null;
        if (group.CourseId != request.CourseId)
            error = await TrainingGroupValidation.ValidateCourseAsync(store, request.CourseId, cancellationToken);
        if (error is null && group.PrimaryInstructorId != request.PrimaryInstructorId)
            error = await TrainingGroupValidation.ValidateInstructorAsync(store, request.PrimaryInstructorId, cancellationToken);
        if (error is not null) return TrainingGroupMutationResult.Error(error.Value);
        if (await store.DuplicateExistsAsync(request.CourseId, request.Name.Trim(), request.PlannedStartDate, id, cancellationToken))
            return TrainingGroupMutationResult.Error(TrainingGroupMutationStatus.DuplicateGroup);

        var now = timeProvider.GetUtcNow();
        var changed = group.UpdateDetails(request.Name, request.PlannedStartDate, request.PlannedEndDate, now);
        changed |= group.ChangeCourse(request.CourseId, now);
        changed |= group.ChangePrimaryInstructor(request.PrimaryInstructorId, now);
        return changed
            ? TrainingGroupMutationResult.FromStore(await store.SaveAsync(group, request.RowVersion, cancellationToken), group)
            : TrainingGroupMutationResult.Success(group);
    }
}
