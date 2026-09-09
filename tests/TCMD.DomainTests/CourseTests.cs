using TCMD.Domain.Courses;

namespace TCMD.DomainTests;

public sealed class CourseTests
{
    [Fact]
    public void Create_NormalizesFieldsAndInitializesState()
    {
        var now = DateTimeOffset.Parse("2026-09-08T10:00:00Z");
        var course = Course.Create("  cs-101/a.b_c  ", "  C# Basics  ", "  Introduction  ", now);

        Assert.NotEqual(Guid.Empty, course.Id);
        Assert.Equal("CS-101/A.B_C", course.Code);
        Assert.Equal("C# Basics", course.Name);
        Assert.Equal("Introduction", course.Description);
        Assert.True(course.IsActive);
        Assert.Equal(now, course.CreatedAtUtc);
        Assert.Equal(now, course.LastUpdatedAtUtc);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Create_RejectsMissingRequiredValues(string value)
    {
        Assert.Throws<ArgumentException>(() => Course.Create(value, "Name", null, DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentException>(() => Course.Create("CODE", value, null, DateTimeOffset.UtcNow));
    }

    [Theory]
    [InlineData("CODE@1")]
    [InlineData("CODE+1")]
    [InlineData("CODE#1")]
    public void Create_RejectsUnsupportedCodeCharacters(string code) =>
        Assert.Throws<ArgumentException>(() => Course.Create(code, "Name", null, DateTimeOffset.UtcNow));

    [Fact]
    public void UpdateDetails_NormalizesChangesAndNoOpPreservesTimestamp()
    {
        var created = DateTimeOffset.Parse("2026-09-08T10:00:00Z");
        var updated = created.AddMinutes(1);
        var course = Course.Create("old", "Old", "Description", created);

        Assert.True(course.UpdateDetails(" new/1 ", " New ", " ", updated));
        Assert.Equal("NEW/1", course.Code);
        Assert.Equal("New", course.Name);
        Assert.Null(course.Description);
        Assert.Equal(updated, course.LastUpdatedAtUtc);
        Assert.False(course.UpdateDetails("new/1", "New", null, updated.AddMinutes(1)));
        Assert.Equal(updated, course.LastUpdatedAtUtc);
        Assert.Equal(created, course.CreatedAtUtc);
    }

    [Fact]
    public void Deactivate_IsIdempotentAndDetailsRemainEditable()
    {
        var created = DateTimeOffset.Parse("2026-09-08T10:00:00Z");
        var deactivated = created.AddMinutes(1);
        var course = Course.Create("CODE", "Name", null, created);

        Assert.True(course.Deactivate(deactivated));
        Assert.False(course.IsActive);
        Assert.False(course.Deactivate(deactivated.AddMinutes(1)));
        Assert.Equal(deactivated, course.LastUpdatedAtUtc);
        Assert.True(course.UpdateDetails("CODE", "Corrected", null, deactivated.AddMinutes(2)));
        Assert.False(course.IsActive);
    }
}
