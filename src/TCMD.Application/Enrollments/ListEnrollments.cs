namespace TCMD.Application.Enrollments;

public sealed record EnrollmentListResult<T>(EnrollmentMutationStatus Status, IReadOnlyList<T>? Enrollments);

public sealed class ListTrainingGroupEnrollments(IEnrollmentStore store)
{
    public async Task<EnrollmentListResult<TrainingGroupEnrollmentDto>> ExecuteAsync(Guid trainingGroupId,
        CancellationToken cancellationToken)
    {
        var reference = await store.GetTrainingGroupReferenceAsync(trainingGroupId, cancellationToken);
        if (!reference.Exists)
            return new(EnrollmentMutationStatus.TrainingGroupNotFound, null);
        var rows = await store.ListByTrainingGroupAsync(trainingGroupId, cancellationToken);
        return new(EnrollmentMutationStatus.Success, rows.Select(TrainingGroupEnrollmentDto.From).ToArray());
    }
}

public sealed class ListStudentEnrollments(IEnrollmentStore store)
{
    public async Task<EnrollmentListResult<StudentEnrollmentDto>> ExecuteAsync(Guid studentId,
        CancellationToken cancellationToken)
    {
        var reference = await store.GetStudentReferenceAsync(studentId, cancellationToken);
        if (!reference.Exists)
            return new(EnrollmentMutationStatus.StudentNotFound, null);
        var rows = await store.ListByStudentAsync(studentId, cancellationToken);
        return new(EnrollmentMutationStatus.Success, rows.Select(StudentEnrollmentDto.From).ToArray());
    }
}
