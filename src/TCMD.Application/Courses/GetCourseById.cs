namespace TCMD.Application.Courses;

public sealed class GetCourseById(ICourseStore store)
{
    public async Task<CourseDto?> ExecuteAsync(Guid id, CancellationToken cancellationToken)
    {
        var course = await store.GetByIdAsync(id, cancellationToken);
        return course is null ? null : CourseDto.From(course);
    }
}
