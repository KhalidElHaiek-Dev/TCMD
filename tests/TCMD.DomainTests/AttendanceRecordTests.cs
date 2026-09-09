using TCMD.Domain.Attendance;

namespace TCMD.DomainTests;

public sealed class AttendanceRecordTests
{
    private static readonly DateTimeOffset RecordedAt = new(2026, 9, 9, 10, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(AttendanceStatus.Present)]
    [InlineData(AttendanceStatus.Absent)]
    [InlineData(AttendanceStatus.Late)]
    [InlineData(AttendanceStatus.Excused)]
    public void Create_InitializesRecordForEveryApprovedStatus(AttendanceStatus status)
    {
        var enrollmentId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var attendance = AttendanceRecord.Create(enrollmentId, sessionId, status, userId, RecordedAt);

        Assert.NotEqual(Guid.Empty, attendance.Id);
        Assert.Equal(enrollmentId, attendance.EnrollmentId);
        Assert.Equal(sessionId, attendance.TrainingSessionId);
        Assert.Equal(status, attendance.Status);
        Assert.Null(attendance.CorrectionNote);
        Assert.Equal(userId, attendance.CreatedByStaffUserId);
        Assert.Equal(userId, attendance.LastUpdatedByStaffUserId);
        Assert.Equal(RecordedAt, attendance.RecordedAtUtc);
        Assert.Equal(RecordedAt, attendance.LastUpdatedAtUtc);
        Assert.Null(attendance.RowVersion);
    }

    [Fact]
    public void Create_RejectsEmptyIdentifiersAndUndefinedStatus()
    {
        var id = Guid.NewGuid();
        Assert.Throws<ArgumentException>(() => AttendanceRecord.Create(Guid.Empty, id, AttendanceStatus.Present, id, RecordedAt));
        Assert.Throws<ArgumentException>(() => AttendanceRecord.Create(id, Guid.Empty, AttendanceStatus.Present, id, RecordedAt));
        Assert.Throws<ArgumentException>(() => AttendanceRecord.Create(id, id, AttendanceStatus.Present, Guid.Empty, RecordedAt));
        Assert.Throws<ArgumentOutOfRangeException>(() => AttendanceRecord.Create(id, id, (AttendanceStatus)99, id, RecordedAt));
    }

    [Fact]
    public void Correct_ChangesMutableValuesAndPreservesCreationAndRelationshipData()
    {
        var attendance = Create();
        var originalId = attendance.Id;
        var originalEnrollment = attendance.EnrollmentId;
        var originalSession = attendance.TrainingSessionId;
        var originalCreator = attendance.CreatedByStaffUserId;
        var updater = Guid.NewGuid();

        Assert.True(attendance.Correct(AttendanceStatus.Late, "  Register review  ", updater, RecordedAt.AddHours(1)));
        Assert.Equal(AttendanceStatus.Late, attendance.Status);
        Assert.Equal("Register review", attendance.CorrectionNote);
        Assert.Equal(updater, attendance.LastUpdatedByStaffUserId);
        Assert.Equal(RecordedAt.AddHours(1), attendance.LastUpdatedAtUtc);
        Assert.Equal(originalId, attendance.Id);
        Assert.Equal(originalEnrollment, attendance.EnrollmentId);
        Assert.Equal(originalSession, attendance.TrainingSessionId);
        Assert.Equal(originalCreator, attendance.CreatedByStaffUserId);
        Assert.Equal(RecordedAt, attendance.RecordedAtUtc);
    }

    [Fact]
    public void Correct_NormalizesBlankAndAcceptsOneThousandCharacters()
    {
        var attendance = Create();
        Assert.True(attendance.Correct(AttendanceStatus.Late, new string('x', 1000), Guid.NewGuid(), RecordedAt.AddHours(1)));
        Assert.Equal(1000, attendance.CorrectionNote!.Length);
        Assert.True(attendance.Correct(AttendanceStatus.Late, "   ", Guid.NewGuid(), RecordedAt.AddHours(2)));
        Assert.Null(attendance.CorrectionNote);
    }

    [Fact]
    public void Correct_RejectsInvalidValues()
    {
        var attendance = Create();
        Assert.Throws<ArgumentException>(() => attendance.Correct(AttendanceStatus.Absent, new string('x', 1001), Guid.NewGuid(), RecordedAt));
        Assert.Throws<ArgumentException>(() => attendance.Correct(AttendanceStatus.Absent, null, Guid.Empty, RecordedAt));
        Assert.Throws<ArgumentOutOfRangeException>(() => attendance.Correct((AttendanceStatus)99, null, Guid.NewGuid(), RecordedAt));
    }

    [Fact]
    public void Correct_NoOpPreservesUpdaterAndTimestamp()
    {
        var attendance = Create();
        var updater = attendance.LastUpdatedByStaffUserId;
        Assert.False(attendance.Correct(AttendanceStatus.Present, "   ", Guid.NewGuid(), RecordedAt.AddHours(1)));
        Assert.Equal(updater, attendance.LastUpdatedByStaffUserId);
        Assert.Equal(RecordedAt, attendance.LastUpdatedAtUtc);
    }

    private static AttendanceRecord Create() => AttendanceRecord.Create(Guid.NewGuid(), Guid.NewGuid(),
        AttendanceStatus.Present, Guid.NewGuid(), RecordedAt);
}
