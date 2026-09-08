using TCMD.Domain.Instructors;

namespace TCMD.Application.Instructors;

public sealed record InstructorDto(Guid Id, string FullName, string? PhoneNumber, string? Email, bool IsActive,
    DateTimeOffset CreatedAtUtc, DateTimeOffset LastUpdatedAtUtc, byte[] RowVersion)
{
    public static InstructorDto From(Instructor instructor) => new(instructor.Id, instructor.FullName,
        instructor.PhoneNumber, instructor.Email, instructor.IsActive, instructor.CreatedAtUtc,
        instructor.LastUpdatedAtUtc, instructor.RowVersion ?? []);
}
