namespace TCMD.Application.Instructors;

public sealed record UpdateInstructorRequest(string FullName, string? PhoneNumber, string? Email, byte[] RowVersion);

public sealed class UpdateInstructor(IInstructorStore store, TimeProvider timeProvider)
{
    public async Task<InstructorMutationResult> ExecuteAsync(Guid id, UpdateInstructorRequest request, CancellationToken cancellationToken)
    {
        var instructor = await store.GetForUpdateAsync(id, cancellationToken);
        if (instructor is null) return InstructorMutationResult.NotFound();
        if (instructor.RowVersion is null || !instructor.RowVersion.SequenceEqual(request.RowVersion))
            return InstructorMutationResult.Conflict();
        if (!instructor.UpdateDetails(request.FullName, request.PhoneNumber, request.Email, timeProvider.GetUtcNow()))
            return InstructorMutationResult.Success(InstructorDto.From(instructor));
        return await store.SaveAsync(instructor, request.RowVersion, cancellationToken)
            ? InstructorMutationResult.Success(InstructorDto.From(instructor)) : InstructorMutationResult.Conflict();
    }
}
