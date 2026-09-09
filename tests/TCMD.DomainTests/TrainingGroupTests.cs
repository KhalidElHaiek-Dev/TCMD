using TCMD.Domain.TrainingGroups;

namespace TCMD.DomainTests;

public sealed class TrainingGroupTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 9, 9, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Start = new(2026, 9, 15);
    private static readonly DateOnly End = new(2026, 12, 15);

    [Fact]
    public void Create_NormalizesAndInitializesPlannedGroup()
    {
        var courseId = Guid.NewGuid();
        var instructorId = Guid.NewGuid();
        var group = TrainingGroup.Create("  Evening Group  ", courseId, instructorId, Start, End, CreatedAt);

        Assert.NotEqual(Guid.Empty, group.Id);
        Assert.Equal("Evening Group", group.Name);
        Assert.Equal(courseId, group.CourseId);
        Assert.Equal(instructorId, group.PrimaryInstructorId);
        Assert.Equal(TrainingGroupStatus.Planned, group.Status);
        Assert.Equal(CreatedAt, group.CreatedAtUtc);
        Assert.Equal(CreatedAt, group.LastUpdatedAtUtc);
    }

    [Fact]
    public void Create_AllowsNoInstructorAndEqualDates()
    {
        var group = TrainingGroup.Create("Group", Guid.NewGuid(), null, Start, Start, CreatedAt);
        Assert.Null(group.PrimaryInstructorId);
        Assert.Equal(Start, group.PlannedEndDate);
    }

    [Fact]
    public void Create_RejectsInvalidRequiredValuesAndDateRange()
    {
        Assert.Throws<ArgumentException>(() => TrainingGroup.Create(" ", Guid.NewGuid(), null, Start, End, CreatedAt));
        Assert.Throws<ArgumentException>(() => TrainingGroup.Create(new string('x', 201), Guid.NewGuid(), null, Start, End, CreatedAt));
        Assert.Throws<ArgumentException>(() => TrainingGroup.Create("Group", Guid.Empty, null, Start, End, CreatedAt));
        Assert.Throws<ArgumentException>(() => TrainingGroup.Create("Group", Guid.NewGuid(), Guid.Empty, Start, End, CreatedAt));
        Assert.Throws<ArgumentException>(() => TrainingGroup.Create("Group", Guid.NewGuid(), null, End, Start, CreatedAt));
    }

    [Fact]
    public void UpdateDetails_RejectsNameLongerThanTwoHundredCharacters()
    {
        var group = TrainingGroup.Create("Group", Guid.NewGuid(), null, Start, End, CreatedAt);

        Assert.Throws<ArgumentException>(() =>
            group.UpdateDetails(new string('x', 201), Start, End, CreatedAt.AddHours(1)));
        Assert.Equal("Group", group.Name);
        Assert.Equal(CreatedAt, group.LastUpdatedAtUtc);
    }

    [Fact]
    public void PlannedGroup_AllowsEveryApprovedEditAndNoOpPreservesTimestamp()
    {
        var group = TrainingGroup.Create("Old", Guid.NewGuid(), null, Start, End, CreatedAt);
        var updatedAt = CreatedAt.AddHours(1);
        var courseId = Guid.NewGuid();
        var instructorId = Guid.NewGuid();

        Assert.True(group.UpdateDetails(" New ", Start.AddDays(1), End.AddDays(1), updatedAt));
        Assert.True(group.ChangeCourse(courseId, updatedAt));
        Assert.True(group.ChangePrimaryInstructor(instructorId, updatedAt));
        Assert.True(group.ChangePrimaryInstructor(null, updatedAt));
        Assert.False(group.UpdateDetails("New", Start.AddDays(1), End.AddDays(1), updatedAt.AddHours(1)));
        Assert.False(group.ChangeCourse(courseId, updatedAt.AddHours(1)));
        Assert.False(group.ChangePrimaryInstructor(null, updatedAt.AddHours(1)));
        Assert.Equal(updatedAt, group.LastUpdatedAtUtc);
        Assert.Equal(CreatedAt, group.CreatedAtUtc);
    }

    [Fact]
    public void Activate_RequiresInstructorAndOnlyWorksFromPlanned()
    {
        var group = TrainingGroup.Create("Group", Guid.NewGuid(), null, Start, End, CreatedAt);
        Assert.Throws<InvalidOperationException>(() => group.Activate(CreatedAt.AddHours(1)));
        group.ChangePrimaryInstructor(Guid.NewGuid(), CreatedAt.AddHours(1));
        Assert.True(group.Activate(CreatedAt.AddHours(2)));
        Assert.Equal(TrainingGroupStatus.Active, group.Status);
        Assert.Throws<InvalidOperationException>(() => group.Activate(CreatedAt.AddHours(3)));
    }

    [Fact]
    public void ActiveGroup_AllowsDetailsAndInstructorReplacementButLocksCourseAndInstructorRemoval()
    {
        var courseId = Guid.NewGuid();
        var group = TrainingGroup.Create("Group", courseId, Guid.NewGuid(), Start, End, CreatedAt);
        group.Activate(CreatedAt.AddHours(1));

        Assert.True(group.UpdateDetails("Updated", Start, End.AddDays(1), CreatedAt.AddHours(2)));
        Assert.True(group.ChangePrimaryInstructor(Guid.NewGuid(), CreatedAt.AddHours(3)));
        Assert.False(group.ChangeCourse(courseId, CreatedAt.AddHours(4)));
        Assert.Throws<InvalidOperationException>(() => group.ChangeCourse(Guid.NewGuid(), CreatedAt.AddHours(4)));
        Assert.Throws<InvalidOperationException>(() => group.ChangePrimaryInstructor(null, CreatedAt.AddHours(4)));
    }

    [Theory]
    [InlineData(TrainingGroupStatus.Completed)]
    [InlineData(TrainingGroupStatus.Cancelled)]
    public void TerminalGroups_RejectEditsAndOutgoingTransitions(TrainingGroupStatus terminal)
    {
        var group = TrainingGroup.Create("Group", Guid.NewGuid(), Guid.NewGuid(), Start, End, CreatedAt);
        group.Activate(CreatedAt.AddHours(1));
        if (terminal == TrainingGroupStatus.Completed) group.Complete(CreatedAt.AddHours(2));
        else group.Cancel(CreatedAt.AddHours(2));

        Assert.Throws<InvalidOperationException>(() => group.UpdateDetails("Changed", Start, End, CreatedAt.AddHours(3)));
        Assert.Throws<InvalidOperationException>(() => group.ChangeCourse(Guid.NewGuid(), CreatedAt.AddHours(3)));
        Assert.Throws<InvalidOperationException>(() => group.ChangePrimaryInstructor(Guid.NewGuid(), CreatedAt.AddHours(3)));
        Assert.Throws<InvalidOperationException>(() => group.Activate(CreatedAt.AddHours(3)));
        if (terminal == TrainingGroupStatus.Completed)
            Assert.Throws<InvalidOperationException>(() => group.Cancel(CreatedAt.AddHours(3)));
        else
            Assert.Throws<InvalidOperationException>(() => group.Complete(CreatedAt.AddHours(3)));
    }

    [Fact]
    public void CompleteAndCancel_AreIdempotentInTheirOwnTerminalState()
    {
        var completed = TrainingGroup.Create("Completed", Guid.NewGuid(), Guid.NewGuid(), Start, End, CreatedAt);
        completed.Activate(CreatedAt.AddHours(1));
        completed.Complete(CreatedAt.AddHours(2));
        Assert.False(completed.Complete(CreatedAt.AddHours(3)));
        Assert.Equal(CreatedAt.AddHours(2), completed.LastUpdatedAtUtc);

        var cancelled = TrainingGroup.Create("Cancelled", Guid.NewGuid(), null, Start, End, CreatedAt);
        cancelled.Cancel(CreatedAt.AddHours(1));
        Assert.False(cancelled.Cancel(CreatedAt.AddHours(2)));
        Assert.Equal(CreatedAt.AddHours(1), cancelled.LastUpdatedAtUtc);
    }

    [Fact]
    public void StatusTransitionMatrix_RejectsSkippedTransitions()
    {
        var planned = TrainingGroup.Create("Planned", Guid.NewGuid(), Guid.NewGuid(), Start, End, CreatedAt);
        Assert.Throws<InvalidOperationException>(() => planned.Complete(CreatedAt.AddHours(1)));

        var active = TrainingGroup.Create("Active", Guid.NewGuid(), Guid.NewGuid(), Start, End, CreatedAt);
        active.Activate(CreatedAt.AddHours(1));
        Assert.True(active.Complete(CreatedAt.AddHours(2)));

        var cancelled = TrainingGroup.Create("Cancel", Guid.NewGuid(), null, Start, End, CreatedAt);
        Assert.True(cancelled.Cancel(CreatedAt.AddHours(1)));
    }
}
