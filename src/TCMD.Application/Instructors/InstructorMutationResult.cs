namespace TCMD.Application.Instructors;

public enum InstructorMutationStatus { Success, NotFound, Conflict }

public sealed record InstructorMutationResult(InstructorMutationStatus Status, InstructorDto? Instructor)
{
    public static InstructorMutationResult Success(InstructorDto instructor) => new(InstructorMutationStatus.Success, instructor);
    public static InstructorMutationResult NotFound() => new(InstructorMutationStatus.NotFound, null);
    public static InstructorMutationResult Conflict() => new(InstructorMutationStatus.Conflict, null);
}
