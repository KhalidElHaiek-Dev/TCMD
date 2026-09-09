namespace TCMD.Application.Courses;

public sealed class DeactivateCourse(ICourseStore store, TimeProvider timeProvider)
{
    public async Task<CourseMutationResult> ExecuteAsync(Guid id, byte[] rowVersion, CancellationToken cancellationToken)
    {
        var course = await store.GetForUpdateAsync(id, cancellationToken);
        if (course is null) return CourseMutationResult.NotFound();
        if (!course.Deactivate(timeProvider.GetUtcNow())) return CourseMutationResult.Success(CourseDto.From(course));
        return CourseMutationResult.FromStore(await store.SaveAsync(course, rowVersion, cancellationToken), course);
    }
}
