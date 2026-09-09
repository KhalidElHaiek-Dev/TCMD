using TCMD.Domain.Enrollments;

namespace TCMD.DomainTests;

public sealed class EnrollmentTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 9, 9, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly EnrollmentDate = new(2026, 9, 9);

    [Fact]
    public void Create_InitializesActiveEnrollment()
    {
        var studentId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var enrollment = Enrollment.Create(studentId, groupId, EnrollmentDate, CreatedAt);

        Assert.NotEqual(Guid.Empty, enrollment.Id);
        Assert.Equal(studentId, enrollment.StudentId);
        Assert.Equal(groupId, enrollment.TrainingGroupId);
        Assert.Equal(EnrollmentDate, enrollment.EnrollmentDate);
        Assert.Equal(EnrollmentStatus.Active, enrollment.Status);
        Assert.Equal(CreatedAt, enrollment.CreatedAtUtc);
        Assert.Equal(CreatedAt, enrollment.LastUpdatedAtUtc);
    }

    [Fact]
    public void Create_RejectsInvalidRequiredValues()
    {
        Assert.Throws<ArgumentException>(() => Enrollment.Create(Guid.Empty, Guid.NewGuid(), EnrollmentDate, CreatedAt));
        Assert.Throws<ArgumentException>(() => Enrollment.Create(Guid.NewGuid(), Guid.Empty, EnrollmentDate, CreatedAt));
        Assert.Throws<ArgumentException>(() => Enrollment.Create(Guid.NewGuid(), Guid.NewGuid(), default, CreatedAt));
    }

    [Fact]
    public void ActiveEnrollment_CanCompleteOrWithdraw()
    {
        var completed = Create();
        var withdrawn = Create();

        Assert.True(completed.Complete(CreatedAt.AddHours(1)));
        Assert.Equal(EnrollmentStatus.Completed, completed.Status);
        Assert.True(withdrawn.Withdraw(CreatedAt.AddHours(1)));
        Assert.Equal(EnrollmentStatus.Withdrawn, withdrawn.Status);
    }

    [Fact]
    public void WithdrawnEnrollment_CanReactivateButCannotComplete()
    {
        var enrollment = Create();
        enrollment.Withdraw(CreatedAt.AddHours(1));

        Assert.Throws<InvalidOperationException>(() => enrollment.Complete(CreatedAt.AddHours(2)));
        Assert.True(enrollment.Reactivate(CreatedAt.AddHours(2)));
        Assert.Equal(EnrollmentStatus.Active, enrollment.Status);
        Assert.Equal(EnrollmentDate, enrollment.EnrollmentDate);
        Assert.Equal(CreatedAt, enrollment.CreatedAtUtc);
    }

    [Fact]
    public void CompletedEnrollment_IsTerminal()
    {
        var enrollment = Create();
        enrollment.Complete(CreatedAt.AddHours(1));

        Assert.Throws<InvalidOperationException>(() => enrollment.Withdraw(CreatedAt.AddHours(2)));
        Assert.Throws<InvalidOperationException>(() => enrollment.Reactivate(CreatedAt.AddHours(2)));
    }

    [Fact]
    public void SameStateCommands_AreNoOpsAndPreserveTimestamp()
    {
        var active = Create();
        Assert.False(active.Reactivate(CreatedAt.AddHours(1)));
        Assert.Equal(CreatedAt, active.LastUpdatedAtUtc);

        var completed = Create();
        completed.Complete(CreatedAt.AddHours(1));
        Assert.False(completed.Complete(CreatedAt.AddHours(2)));
        Assert.Equal(CreatedAt.AddHours(1), completed.LastUpdatedAtUtc);

        var withdrawn = Create();
        withdrawn.Withdraw(CreatedAt.AddHours(1));
        Assert.False(withdrawn.Withdraw(CreatedAt.AddHours(2)));
        Assert.Equal(CreatedAt.AddHours(1), withdrawn.LastUpdatedAtUtc);
    }

    private static Enrollment Create() =>
        Enrollment.Create(Guid.NewGuid(), Guid.NewGuid(), EnrollmentDate, CreatedAt);
}
