using TCMD.Domain.Students;

namespace TCMD.Application.Students;

public sealed record StudentDto(
    Guid Id,
    string StudentNumber,
    string FullName,
    string PhoneNumber,
    string? Email,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset LastUpdatedAtUtc,
    byte[] RowVersion)
{
    public static StudentDto From(Student student) => new(
        student.Id,
        student.StudentNumber,
        student.FullName,
        student.PhoneNumber,
        student.Email,
        student.IsActive,
        student.CreatedAtUtc,
        student.LastUpdatedAtUtc,
        student.RowVersion ?? []);
}
