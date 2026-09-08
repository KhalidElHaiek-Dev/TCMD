namespace TCMD.Application.Instructors;

public sealed class DeactivateInstructor(IInstructorStore store, TimeProvider timeProvider)
{
    public async Task<InstructorMutationResult> ExecuteAsync(Guid id, byte[] rowVersion, CancellationToken cancellationToken)
    {
        var instructor = await store.GetForUpdateAsync(id, cancellationToken);
        if (instructor is null) return InstructorMutationResult.NotFound();
        if (!instructor.Deactivate(timeProvider.GetUtcNow())) return InstructorMutationResult.Success(InstructorDto.From(instructor));
        return await store.SaveAsync(instructor, rowVersion, cancellationToken)
            ? InstructorMutationResult.Success(InstructorDto.From(instructor)) : InstructorMutationResult.Conflict();
    }
}
