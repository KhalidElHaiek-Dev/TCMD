namespace TCMD.Application.Students;

public sealed class SearchStudents(IStudentStore studentStore)
{
    public async Task<IReadOnlyList<StudentDto>> ExecuteAsync(
        string? search,
        bool? isActive,
        CancellationToken cancellationToken)
    {
        var students = await studentStore.SearchAsync(
            string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
            isActive,
            cancellationToken);

        return students.Select(StudentDto.From).ToArray();
    }
}
