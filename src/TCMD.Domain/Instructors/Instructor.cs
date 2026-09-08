using System.Net.Mail;

namespace TCMD.Domain.Instructors;

public sealed class Instructor
{
    private Instructor() { }

    private Instructor(Guid id, string fullName, string? phoneNumber, string? email, DateTimeOffset createdAtUtc)
    {
        Id = id;
        FullName = RequireValue(fullName, nameof(fullName));
        PhoneNumber = NormalizeOptional(phoneNumber);
        Email = NormalizeEmail(email);
        IsActive = true;
        CreatedAtUtc = createdAtUtc;
        LastUpdatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public string FullName { get; private set; } = string.Empty;
    public string? PhoneNumber { get; private set; }
    public string? Email { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset LastUpdatedAtUtc { get; private set; }
    public byte[]? RowVersion { get; private set; }

    public static Instructor Create(string fullName, string? phoneNumber, string? email, DateTimeOffset createdAtUtc) =>
        new(Guid.NewGuid(), fullName, phoneNumber, email, createdAtUtc);

    public bool UpdateDetails(string fullName, string? phoneNumber, string? email, DateTimeOffset updatedAtUtc)
    {
        var normalizedFullName = RequireValue(fullName, nameof(fullName));
        var normalizedPhoneNumber = NormalizeOptional(phoneNumber);
        var normalizedEmail = NormalizeEmail(email);
        if (FullName == normalizedFullName && PhoneNumber == normalizedPhoneNumber && Email == normalizedEmail)
        {
            return false;
        }

        FullName = normalizedFullName;
        PhoneNumber = normalizedPhoneNumber;
        Email = normalizedEmail;
        LastUpdatedAtUtc = updatedAtUtc;
        return true;
    }

    public bool Deactivate(DateTimeOffset updatedAtUtc)
    {
        if (!IsActive) return false;
        IsActive = false;
        LastUpdatedAtUtc = updatedAtUtc;
        return true;
    }

    private static string RequireValue(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A value is required.", parameterName);
        return value.Trim();
    }

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeEmail(string? value)
    {
        var email = NormalizeOptional(value);
        if (email is not null && (!MailAddress.TryCreate(email, out var parsed) || parsed.Address != email))
        {
            throw new ArgumentException("Email must be a valid email address.", nameof(value));
        }
        return email;
    }
}
