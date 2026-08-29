namespace TCMD.Application.Students;

public sealed record RegisterStudentRequest(string FullName, string PhoneNumber, string? Email);

public sealed class RegisterStudent(
    IStudentStore studentStore,
    IStudentNumberGenerator studentNumberGenerator,
    TimeProvider timeProvider)
{
    public async Task<StudentDto> ExecuteAsync(
        RegisterStudentRequest request,
        CancellationToken cancellationToken)
    {
        var studentNumber = await studentNumberGenerator.NextAsync(cancellationToken);
        var student = Domain.Students.Student.Register(
            studentNumber,
            request.FullName,
            request.PhoneNumber,
            request.Email,
            timeProvider.GetUtcNow());

        await studentStore.AddAsync(student, cancellationToken);
        return StudentDto.From(student);
    }
}
