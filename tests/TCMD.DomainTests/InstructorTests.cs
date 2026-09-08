using TCMD.Domain.Instructors;

namespace TCMD.DomainTests;

public sealed class InstructorTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 9, 8, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_NormalizesFieldsAndInitializesActiveStateAndTimestamps()
    {
        var instructor = Instructor.Create("  Ada Lovelace  ", "  +212600000000  ", "  ada@example.com  ", CreatedAt);
        Assert.NotEqual(Guid.Empty, instructor.Id);
        Assert.Equal("Ada Lovelace", instructor.FullName);
        Assert.Equal("+212600000000", instructor.PhoneNumber);
        Assert.Equal("ada@example.com", instructor.Email);
        Assert.True(instructor.IsActive);
        Assert.Equal(CreatedAt, instructor.CreatedAtUtc);
        Assert.Equal(CreatedAt, instructor.LastUpdatedAtUtc);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Create_BlankFullName_Throws(string value) =>
        Assert.Throws<ArgumentException>(() => Instructor.Create(value, null, null, CreatedAt));

    [Fact]
    public void Create_BlankOptionalContactValues_BecomeNull()
    {
        var instructor = Instructor.Create("Ada", "  ", "", CreatedAt);
        Assert.Null(instructor.PhoneNumber);
        Assert.Null(instructor.Email);
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("a@")]
    public void Create_InvalidEmail_Throws(string email) =>
        Assert.Throws<ArgumentException>(() => Instructor.Create("Ada", null, email, CreatedAt));

    [Fact]
    public void UpdateDetails_ChangesContactDetailsAndPreservesIdentityAndCreationTime()
    {
        var instructor = Instructor.Create("Original", null, null, CreatedAt);
        var id = instructor.Id;
        var updatedAt = CreatedAt.AddHours(1);
        Assert.True(instructor.UpdateDetails(" Updated ", " phone ", "updated@example.com", updatedAt));
        Assert.Equal(id, instructor.Id);
        Assert.Equal(CreatedAt, instructor.CreatedAtUtc);
        Assert.Equal(updatedAt, instructor.LastUpdatedAtUtc);
        Assert.Equal("Updated", instructor.FullName);
        Assert.Equal("phone", instructor.PhoneNumber);
    }

    [Fact]
    public void UpdateDetails_WhenNothingChanges_DoesNotChangeTimestamp()
    {
        var instructor = Instructor.Create("Ada", null, null, CreatedAt);
        Assert.False(instructor.UpdateDetails(" Ada ", " ", null, CreatedAt.AddHours(1)));
        Assert.Equal(CreatedAt, instructor.LastUpdatedAtUtc);
    }

    [Fact]
    public void InactiveInstructor_CanHaveHistoricalDetailsCorrected()
    {
        var instructor = Instructor.Create("Ada", null, null, CreatedAt);
        instructor.Deactivate(CreatedAt.AddHours(1));
        Assert.True(instructor.UpdateDetails("Corrected", null, null, CreatedAt.AddHours(2)));
        Assert.False(instructor.IsActive);
        Assert.Equal("Corrected", instructor.FullName);
    }

    [Fact]
    public void Deactivate_IsIdempotentAndChangesTimestampOnlyOnce()
    {
        var instructor = Instructor.Create("Ada", null, null, CreatedAt);
        var first = CreatedAt.AddHours(1);
        Assert.True(instructor.Deactivate(first));
        Assert.False(instructor.Deactivate(CreatedAt.AddHours(2)));
        Assert.False(instructor.IsActive);
        Assert.Equal(first, instructor.LastUpdatedAtUtc);
    }
}
