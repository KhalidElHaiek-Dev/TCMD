namespace TCMD.Application.Courses;

public sealed record CreateCourseRequest(string Code, string Name, string? Description);

public sealed class CreateCourse(ICourseStore store, TimeProvider timeProvider)
{
    public async Task<CourseMutationResult> ExecuteAsync(CreateCourseRequest request, CancellationToken cancellationToken)
    {
        var course = Domain.Courses.Course.Create(request.Code, request.Name, request.Description, timeProvider.GetUtcNow());
        return CourseMutationResult.FromStore(await store.AddAsync(course, cancellationToken), course);
    }
}
