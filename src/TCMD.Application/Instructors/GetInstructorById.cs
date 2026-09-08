namespace TCMD.Application.Instructors;

public sealed class GetInstructorById(IInstructorStore store)
{
    public async Task<InstructorDto?> ExecuteAsync(Guid id, CancellationToken cancellationToken)
    {
        var instructor = await store.GetByIdAsync(id, cancellationToken);
        return instructor is null ? null : InstructorDto.From(instructor);
    }
}
