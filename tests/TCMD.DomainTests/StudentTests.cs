using TCMD.Domain.Students;

namespace TCMD.DomainTests;

public sealed class StudentTests
{
    [Fact]
    public void Register_WithApprovedFields_CreatesActiveStudentAndNormalizesText()
    {
        var now = new DateTimeOffset(2026, 8, 11, 10, 0, 0, TimeSpan.Zero);

        var student = Student.Register("STU-000001", "  Ada Lovelace  ", "  +212600000000  ", "  ada@example.com  ", now);

        Assert.NotEqual(Guid.Empty, student.Id);
        Assert.Equal("STU-000001", student.StudentNumber);
        Assert.Equal("Ada Lovelace", student.FullName);
        Assert.Equal("+212600000000", student.PhoneNumber);
        Assert.Equal("ada@example.com", student.Email);
        Assert.True(student.IsActive);
        Assert.Equal(now, student.CreatedAtUtc);
        Assert.Equal(now, student.LastUpdatedAtUtc);
    }

    [Theory]
    [InlineData("", "+212600000000")]
    [InlineData("Ada Lovelace", "  ")]
    public void Register_WhenRequiredFieldIsBlank_Throws(string fullName, string phoneNumber)
    {
        Assert.Throws<ArgumentException>(() =>
            Student.Register("STU-000001", fullName, phoneNumber, null, DateTimeOffset.UtcNow));
    }

    [Theory]
    [InlineData("STU-1")]
    [InlineData("STD-000001")]
    [InlineData("STU-0000001")]
    public void Register_WhenStudentNumberHasWrongFormat_Throws(string studentNumber)
    {
        Assert.Throws<ArgumentException>(() =>
            Student.Register(studentNumber, "Ada Lovelace", "+212600000000", null, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void UpdateDetails_NormalizesValuesAndPreservesIdentityAndCreationTime()
    {
        var createdAt = new DateTimeOffset(2026, 8, 11, 10, 0, 0, TimeSpan.Zero);
        var updatedAt = createdAt.AddHours(1);
        var student = Student.Register("STU-000001", "Original", "+212600000000", "original@example.com", createdAt);

        student.UpdateDetails("  Updated Name  ", "  +212611111111  ", "  ", updatedAt);

        Assert.Equal("STU-000001", student.StudentNumber);
        Assert.Equal("Updated Name", student.FullName);
        Assert.Equal("+212611111111", student.PhoneNumber);
        Assert.Null(student.Email);
        Assert.Equal(createdAt, student.CreatedAtUtc);
        Assert.Equal(updatedAt, student.LastUpdatedAtUtc);
    }

    [Theory]
    [InlineData("", "+212600000000")]
    [InlineData("Ada Lovelace", "  ")]
    public void UpdateDetails_WhenRequiredFieldIsBlank_Throws(string fullName, string phoneNumber)
    {
        var student = Student.Register("STU-000001", "Original", "+212600000000", null, DateTimeOffset.UtcNow);

        Assert.Throws<ArgumentException>(() =>
            student.UpdateDetails(fullName, phoneNumber, null, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Deactivate_IsIdempotentAndDoesNotChangeTimestampTwice()
    {
        var createdAt = new DateTimeOffset(2026, 8, 11, 10, 0, 0, TimeSpan.Zero);
        var deactivatedAt = createdAt.AddHours(1);
        var student = Student.Register("STU-000001", "Ada Lovelace", "+212600000000", null, createdAt);

        Assert.True(student.Deactivate(deactivatedAt));
        Assert.False(student.Deactivate(deactivatedAt.AddHours(1)));

        Assert.False(student.IsActive);
        Assert.Equal(deactivatedAt, student.LastUpdatedAtUtc);
        Assert.Equal(createdAt, student.CreatedAtUtc);
    }
}
