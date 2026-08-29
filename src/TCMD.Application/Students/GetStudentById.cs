namespace TCMD.Application.Students;

public sealed class GetStudentById(IStudentStore studentStore)
{
    public async Task<StudentDto?> ExecuteAsync(Guid id, CancellationToken cancellationToken)
    {
        var student = await studentStore.GetByIdAsync(id, cancellationToken);
        return student is null ? null : StudentDto.From(student);
    }
}
