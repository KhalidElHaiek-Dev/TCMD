namespace TCMD.Application.Instructors;

public sealed record CreateInstructorRequest(string FullName, string? PhoneNumber, string? Email);

public sealed class CreateInstructor(IInstructorStore store, TimeProvider timeProvider)
{
    public async Task<InstructorDto> ExecuteAsync(CreateInstructorRequest request, CancellationToken cancellationToken)
    {
        var instructor = Domain.Instructors.Instructor.Create(request.FullName, request.PhoneNumber, request.Email, timeProvider.GetUtcNow());
        await store.AddAsync(instructor, cancellationToken);
        return InstructorDto.From(instructor);
    }
}
