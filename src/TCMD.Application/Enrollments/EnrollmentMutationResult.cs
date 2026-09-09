using TCMD.Domain.Enrollments;

namespace TCMD.Application.Enrollments;

public enum EnrollmentMutationStatus
{
    Success, EnrollmentNotFound, StudentNotFound, StudentInactive, TrainingGroupNotFound,
    TrainingGroupIneligible, DuplicateEnrollment, InvalidStatusTransition, ConcurrencyConflict
}

public sealed record EnrollmentMutationResult(EnrollmentMutationStatus Status, EnrollmentDto? Enrollment)
{
    public static EnrollmentMutationResult Success(Enrollment enrollment) =>
        new(EnrollmentMutationStatus.Success, EnrollmentDto.From(enrollment));
    public static EnrollmentMutationResult Error(EnrollmentMutationStatus status) => new(status, null);
    public static EnrollmentMutationResult FromStore(EnrollmentStoreSaveStatus status, Enrollment enrollment) => status switch
    {
        EnrollmentStoreSaveStatus.Success => Success(enrollment),
        EnrollmentStoreSaveStatus.ConcurrencyConflict => Error(EnrollmentMutationStatus.ConcurrencyConflict),
        EnrollmentStoreSaveStatus.DuplicateEnrollment => Error(EnrollmentMutationStatus.DuplicateEnrollment),
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };
}
