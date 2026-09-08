namespace TCMD.Domain.Students;

public sealed class Student
{
    private Student()
    {
    }

    private Student(
        Guid id,
        string studentNumber,
        string fullName,
        string phoneNumber,
        string? email,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        StudentNumber = RequireValue(studentNumber, nameof(studentNumber));
        FullName = RequireValue(fullName, nameof(fullName));
        PhoneNumber = RequireValue(phoneNumber, nameof(phoneNumber));
        Email = NormalizeOptional(email);
        IsActive = true;
        CreatedAtUtc = createdAtUtc;
        LastUpdatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public string StudentNumber { get; private set; } = string.Empty;
    public string FullName { get; private set; } = string.Empty;
    public string PhoneNumber { get; private set; } = string.Empty;
    public string? Email { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset LastUpdatedAtUtc { get; private set; }
    public byte[]? RowVersion { get; private set; }

    public static Student Register(
        string studentNumber,
        string fullName,
        string phoneNumber,
        string? email,
        DateTimeOffset createdAtUtc)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(studentNumber, "^STU-[0-9]{6}$"))
        {
            throw new ArgumentException("Student number must use the format STU-000001.", nameof(studentNumber));
        }

        return new Student(Guid.NewGuid(), studentNumber, fullName, phoneNumber, email, createdAtUtc);
    }

    public void UpdateDetails(
        string fullName,
        string phoneNumber,
        string? email,
        DateTimeOffset updatedAtUtc)
    {
        var normalizedFullName = RequireValue(fullName, nameof(fullName));
        var normalizedPhoneNumber = RequireValue(phoneNumber, nameof(phoneNumber));
        var normalizedEmail = NormalizeOptional(email);

        FullName = normalizedFullName;
        PhoneNumber = normalizedPhoneNumber;
        Email = normalizedEmail;
        LastUpdatedAtUtc = updatedAtUtc;
    }

    public bool Deactivate(DateTimeOffset updatedAtUtc)
    {
        if (!IsActive)
        {
            return false;
        }

        IsActive = false;
        LastUpdatedAtUtc = updatedAtUtc;
        return true;
    }

    private static string RequireValue(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A value is required.", parameterName);
        }

        return value.Trim();
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
