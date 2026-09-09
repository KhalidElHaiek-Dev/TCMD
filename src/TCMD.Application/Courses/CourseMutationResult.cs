using TCMD.Domain.Courses;

namespace TCMD.Application.Courses;

public enum CourseMutationStatus { Success, NotFound, ConcurrencyConflict, DuplicateCode }

public sealed record CourseMutationResult(CourseMutationStatus Status, CourseDto? Course)
{
    public static CourseMutationResult Success(CourseDto course) => new(CourseMutationStatus.Success, course);
    public static CourseMutationResult NotFound() => new(CourseMutationStatus.NotFound, null);
    public static CourseMutationResult ConcurrencyConflict() => new(CourseMutationStatus.ConcurrencyConflict, null);
    public static CourseMutationResult DuplicateCode() => new(CourseMutationStatus.DuplicateCode, null);

    public static CourseMutationResult FromStore(CourseStoreSaveStatus status, Course course) => status switch
    {
        CourseStoreSaveStatus.Success => Success(CourseDto.From(course)),
        CourseStoreSaveStatus.ConcurrencyConflict => ConcurrencyConflict(),
        CourseStoreSaveStatus.DuplicateCode => DuplicateCode(),
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };
}
