using TCMD.Domain.TrainingGroups;

namespace TCMD.Application.TrainingGroups;

internal static class TrainingGroupValidation
{
    public static async Task<TrainingGroupMutationStatus?> ValidateCourseAsync(ITrainingGroupStore store, Guid id,
        CancellationToken cancellationToken)
    {
        var reference = await store.GetCourseReferenceAsync(id, cancellationToken);
        if (!reference.Exists) return TrainingGroupMutationStatus.CourseNotFound;
        return reference.IsActive ? null : TrainingGroupMutationStatus.CourseInactive;
    }

    public static async Task<TrainingGroupMutationStatus?> ValidateInstructorAsync(ITrainingGroupStore store, Guid? id,
        CancellationToken cancellationToken)
    {
        if (id is null) return null;
        var reference = await store.GetInstructorReferenceAsync(id.Value, cancellationToken);
        if (!reference.Exists) return TrainingGroupMutationStatus.InstructorNotFound;
        return reference.IsActive ? null : TrainingGroupMutationStatus.InstructorInactive;
    }

    public static bool HasExpectedVersion(TrainingGroup group, byte[] expected) =>
        group.RowVersion is not null && group.RowVersion.SequenceEqual(expected);
}
