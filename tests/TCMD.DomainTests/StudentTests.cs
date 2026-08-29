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
}
