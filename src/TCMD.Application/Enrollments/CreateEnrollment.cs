namespace TCMD.Application.Enrollments;

public sealed class CreateEnrollment(IEnrollmentStore store, TimeProvider timeProvider)
{
    public async Task<EnrollmentMutationResult> ExecuteAsync(Guid trainingGroupId, Guid studentId,
        CancellationToken cancellationToken)
    {
        var error = await EnrollmentValidation.ValidateStudentAsync(store, studentId, cancellationToken)
            ?? await EnrollmentValidation.ValidateGroupAsync(store, trainingGroupId, cancellationToken);
        if (error is not null) return EnrollmentMutationResult.Error(error.Value);
        if (await store.ExistsAsync(studentId, trainingGroupId, cancellationToken))
            return EnrollmentMutationResult.Error(EnrollmentMutationStatus.DuplicateEnrollment);

        var now = timeProvider.GetUtcNow();
        var enrollment = Domain.Enrollments.Enrollment.Create(studentId, trainingGroupId,
            DateOnly.FromDateTime(now.UtcDateTime), now);
        return EnrollmentMutationResult.FromStore(await store.AddAsync(enrollment, cancellationToken), enrollment);
    }
}
