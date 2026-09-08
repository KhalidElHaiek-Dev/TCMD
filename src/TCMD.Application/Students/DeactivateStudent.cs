namespace TCMD.Application.Students;

public sealed class DeactivateStudent(IStudentStore studentStore, TimeProvider timeProvider)
{
    public async Task<StudentMutationResult> ExecuteAsync(
        Guid id,
        byte[] rowVersion,
        CancellationToken cancellationToken)
    {
        var student = await studentStore.GetForUpdateAsync(id, cancellationToken);
        if (student is null)
        {
            return StudentMutationResult.NotFound();
        }

        if (!student.Deactivate(timeProvider.GetUtcNow()))
        {
            return StudentMutationResult.Success(StudentDto.From(student));
        }

        return await studentStore.SaveAsync(student, rowVersion, cancellationToken)
            ? StudentMutationResult.Success(StudentDto.From(student))
            : StudentMutationResult.Conflict();
    }
}
