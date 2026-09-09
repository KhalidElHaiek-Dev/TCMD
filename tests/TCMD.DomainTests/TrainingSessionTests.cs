using TCMD.Domain.TrainingSessions;

namespace TCMD.DomainTests;

public sealed class TrainingSessionTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 9, 9, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Date = new(2026, 9, 15);
    private static readonly TimeOnly Start = new(9, 0);
    private static readonly TimeOnly End = new(11, 0);

    [Fact]
    public void Create_InitializesScheduledSessionAndNormalizesLocation()
    {
        var groupId = Guid.NewGuid();
        var session = TrainingSession.Create(groupId, Date, Start, End, "  Room A  ", CreatedAt);
        Assert.NotEqual(Guid.Empty, session.Id);
        Assert.Equal(groupId, session.TrainingGroupId);
        Assert.Equal(TrainingSessionStatus.Scheduled, session.Status);
        Assert.Equal("Room A", session.Location);
        Assert.Equal(CreatedAt, session.CreatedAtUtc);
        Assert.Equal(CreatedAt, session.LastUpdatedAtUtc);
    }

    [Fact]
    public void Create_BlankLocationBecomesNullAndFiveHundredCharactersIsAccepted()
    {
        Assert.Null(Create("   ").Location);
        Assert.Equal(500, Create(new string('x', 500)).Location!.Length);
    }

    [Fact]
    public void Create_RejectsInvalidRequiredValuesAndOverlongLocation()
    {
        Assert.Throws<ArgumentException>(() => TrainingSession.Create(Guid.Empty, Date, Start, End, null, CreatedAt));
        Assert.Throws<ArgumentException>(() => TrainingSession.Create(Guid.NewGuid(), default, Start, End, null, CreatedAt));
        Assert.Throws<ArgumentException>(() => Create(new string('x', 501)));
    }

    [Fact]
    public void Create_RejectsEqualReversedAndOvernightTimes()
    {
        Assert.Throws<ArgumentException>(() => TrainingSession.Create(Guid.NewGuid(), Date, Start, Start, null, CreatedAt));
        Assert.Throws<ArgumentException>(() => TrainingSession.Create(Guid.NewGuid(), Date, End, Start, null, CreatedAt));
        Assert.Throws<ArgumentException>(() => TrainingSession.Create(Guid.NewGuid(), Date, new(23, 0), new(1, 0), null, CreatedAt));
    }

    [Fact]
    public void UpdateDetails_ChangesValuesAndNormalizesLocation()
    {
        var session = Create("Room A");
        Assert.True(session.UpdateDetails(Date.AddDays(1), new(10, 0), new(12, 0), "  Room B ", CreatedAt.AddHours(1)));
        Assert.Equal(Date.AddDays(1), session.SessionDate);
        Assert.Equal("Room B", session.Location);
        Assert.Equal(CreatedAt.AddHours(1), session.LastUpdatedAtUtc);
    }

    [Fact]
    public void UpdateDetails_NoOpPreservesTimestampAndInvalidRangeIsRejected()
    {
        var session = Create("Room A");
        Assert.False(session.UpdateDetails(Date, Start, End, " Room A ", CreatedAt.AddHours(1)));
        Assert.Equal(CreatedAt, session.LastUpdatedAtUtc);
        Assert.Throws<ArgumentException>(() => session.UpdateDetails(Date, End, Start, null, CreatedAt.AddHours(1)));
    }

    [Fact]
    public void Complete_IsTerminalAndIdempotent()
    {
        var session = Create();
        Assert.True(session.Complete(CreatedAt.AddHours(1)));
        Assert.False(session.Complete(CreatedAt.AddHours(2)));
        Assert.Equal(CreatedAt.AddHours(1), session.LastUpdatedAtUtc);
        Assert.Throws<InvalidOperationException>(() => session.Cancel(CreatedAt.AddHours(2)));
        Assert.Throws<InvalidOperationException>(() => session.UpdateDetails(Date, Start, End, null, CreatedAt.AddHours(2)));
    }

    [Fact]
    public void Cancel_IsTerminalAndIdempotent()
    {
        var session = Create();
        Assert.True(session.Cancel(CreatedAt.AddHours(1)));
        Assert.False(session.Cancel(CreatedAt.AddHours(2)));
        Assert.Equal(CreatedAt.AddHours(1), session.LastUpdatedAtUtc);
        Assert.Throws<InvalidOperationException>(() => session.Complete(CreatedAt.AddHours(2)));
        Assert.Throws<InvalidOperationException>(() => session.UpdateDetails(Date, Start, End, null, CreatedAt.AddHours(2)));
    }

    private static TrainingSession Create(string? location = null) =>
        TrainingSession.Create(Guid.NewGuid(), Date, Start, End, location, CreatedAt);
}
