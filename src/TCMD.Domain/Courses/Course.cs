namespace TCMD.Domain.Courses;

public sealed class Course
{
    private Course() { }

    private Course(Guid id, string code, string name, string? description, DateTimeOffset createdAtUtc)
    {
        Id = id;
        Code = NormalizeCode(code);
        Name = RequireValue(name, nameof(name));
        Description = NormalizeOptional(description);
        IsActive = true;
        CreatedAtUtc = createdAtUtc;
        LastUpdatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset LastUpdatedAtUtc { get; private set; }
    public byte[]? RowVersion { get; private set; }

    public static Course Create(string code, string name, string? description, DateTimeOffset createdAtUtc) =>
        new(Guid.NewGuid(), code, name, description, createdAtUtc);

    public bool UpdateDetails(string code, string name, string? description, DateTimeOffset updatedAtUtc)
    {
        var normalizedCode = NormalizeCode(code);
        var normalizedName = RequireValue(name, nameof(name));
        var normalizedDescription = NormalizeOptional(description);
        if (Code == normalizedCode && Name == normalizedName && Description == normalizedDescription) return false;

        Code = normalizedCode;
        Name = normalizedName;
        Description = normalizedDescription;
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

    private static string NormalizeCode(string value)
    {
        var code = RequireValue(value, nameof(value)).ToUpperInvariant();
        if (code.Any(character => !char.IsLetterOrDigit(character) && character is not (' ' or '-' or '_' or '.' or '/')))
            throw new ArgumentException("Code contains an unsupported character.", nameof(value));
        return code;
    }

    private static string RequireValue(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A value is required.", parameterName);
        return value.Trim();
    }

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
