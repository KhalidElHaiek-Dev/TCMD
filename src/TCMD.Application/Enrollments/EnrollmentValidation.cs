using TCMD.Domain.Enrollments;
using TCMD.Domain.TrainingGroups;

namespace TCMD.Application.Enrollments;

internal static class EnrollmentValidation
{
    public static async Task<EnrollmentMutationStatus?> ValidateStudentAsync(IEnrollmentStore store, Guid id,
        CancellationToken cancellationToken)
    {
        var reference = await store.GetStudentReferenceAsync(id, cancellationToken);
        if (!reference.Exists) return EnrollmentMutationStatus.StudentNotFound;
        return reference.IsActive ? null : EnrollmentMutationStatus.StudentInactive;
    }

    public static async Task<EnrollmentMutationStatus?> ValidateGroupAsync(IEnrollmentStore store, Guid id,
        CancellationToken cancellationToken)
    {
        var reference = await store.GetTrainingGroupReferenceAsync(id, cancellationToken);
        if (!reference.Exists) return EnrollmentMutationStatus.TrainingGroupNotFound;
        return reference.Status is TrainingGroupStatus.Planned or TrainingGroupStatus.Active
            ? null : EnrollmentMutationStatus.TrainingGroupIneligible;
    }

    public static bool HasExpectedVersion(Enrollment enrollment, byte[] expected) =>
        enrollment.RowVersion is not null && enrollment.RowVersion.SequenceEqual(expected);
}
