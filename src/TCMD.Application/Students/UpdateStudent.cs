namespace TCMD.Application.Students;

public sealed record UpdateStudentRequest(
    string FullName,
    string PhoneNumber,
    string? Email,
    byte[] RowVersion);

public sealed class UpdateStudent(IStudentStore studentStore, TimeProvider timeProvider)
{
    public async Task<StudentMutationResult> ExecuteAsync(
        Guid id,
        UpdateStudentRequest request,
        CancellationToken cancellationToken)
    {
        var student = await studentStore.GetForUpdateAsync(id, cancellationToken);
        if (student is null)
        {
            return StudentMutationResult.NotFound();
        }

        student.UpdateDetails(request.FullName, request.PhoneNumber, request.Email, timeProvider.GetUtcNow());
        return await studentStore.SaveAsync(student, request.RowVersion, cancellationToken)
            ? StudentMutationResult.Success(StudentDto.From(student))
            : StudentMutationResult.Conflict();
    }
}
