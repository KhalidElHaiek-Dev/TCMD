namespace TCMD.Application.Students;

public enum StudentMutationStatus
{
    Success,
    NotFound,
    Conflict
}

public sealed record StudentMutationResult(StudentMutationStatus Status, StudentDto? Student)
{
    public static StudentMutationResult Success(StudentDto student) => new(StudentMutationStatus.Success, student);
    public static StudentMutationResult NotFound() => new(StudentMutationStatus.NotFound, null);
    public static StudentMutationResult Conflict() => new(StudentMutationStatus.Conflict, null);
}
