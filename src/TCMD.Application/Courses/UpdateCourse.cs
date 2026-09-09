namespace TCMD.Application.Courses;

public sealed record UpdateCourseRequest(string Code, string Name, string? Description, byte[] RowVersion);

public sealed class UpdateCourse(ICourseStore store, TimeProvider timeProvider)
{
    public async Task<CourseMutationResult> ExecuteAsync(Guid id, UpdateCourseRequest request, CancellationToken cancellationToken)
    {
        var course = await store.GetForUpdateAsync(id, cancellationToken);
        if (course is null) return CourseMutationResult.NotFound();
        if (course.RowVersion is null || !course.RowVersion.SequenceEqual(request.RowVersion))
            return CourseMutationResult.ConcurrencyConflict();
        if (!course.UpdateDetails(request.Code, request.Name, request.Description, timeProvider.GetUtcNow()))
            return CourseMutationResult.Success(CourseDto.From(course));
        return CourseMutationResult.FromStore(await store.SaveAsync(course, request.RowVersion, cancellationToken), course);
    }
}
