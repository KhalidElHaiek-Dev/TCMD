namespace TCMD.Application.Instructors;

public sealed class SearchInstructors(IInstructorStore store)
{
    public async Task<IReadOnlyList<InstructorDto>> ExecuteAsync(string? search, bool? isActive, CancellationToken cancellationToken)
    {
        var instructors = await store.SearchAsync(string.IsNullOrWhiteSpace(search) ? null : search.Trim(), isActive, cancellationToken);
        return instructors.Select(InstructorDto.From).ToArray();
    }
}
