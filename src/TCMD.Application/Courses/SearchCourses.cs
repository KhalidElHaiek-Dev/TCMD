namespace TCMD.Application.Courses;

public sealed class SearchCourses(ICourseStore store)
{
    public async Task<IReadOnlyList<CourseDto>> ExecuteAsync(string? search, bool? isActive, CancellationToken cancellationToken)
    {
        var courses = await store.SearchAsync(string.IsNullOrWhiteSpace(search) ? null : search.Trim(), isActive, cancellationToken);
        return courses.Select(CourseDto.From).ToArray();
    }
}
