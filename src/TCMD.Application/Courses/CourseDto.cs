using TCMD.Domain.Courses;

namespace TCMD.Application.Courses;

public sealed record CourseDto(Guid Id, string Code, string Name, string? Description, bool IsActive,
    DateTimeOffset CreatedAtUtc, DateTimeOffset LastUpdatedAtUtc, byte[] RowVersion)
{
    public static CourseDto From(Course course) => new(course.Id, course.Code, course.Name, course.Description,
        course.IsActive, course.CreatedAtUtc, course.LastUpdatedAtUtc, course.RowVersion ?? []);
}
