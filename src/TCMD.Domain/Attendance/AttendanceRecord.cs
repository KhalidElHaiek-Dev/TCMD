namespace TCMD.Domain.Attendance;

public sealed class AttendanceRecord
{
    public const int CorrectionNoteMaxLength = 1000;

    private AttendanceRecord(Guid id, Guid enrollmentId, Guid trainingSessionId, AttendanceStatus status,
        Guid createdByStaffUserId, DateTimeOffset recordedAtUtc)
    {
        if (id == Guid.Empty) throw new ArgumentException("Attendance id is required.", nameof(id));
        if (enrollmentId == Guid.Empty) throw new ArgumentException("Enrollment id is required.", nameof(enrollmentId));
        if (trainingSessionId == Guid.Empty) throw new ArgumentException("Training session id is required.", nameof(trainingSessionId));
        if (createdByStaffUserId == Guid.Empty) throw new ArgumentException("Staff user id is required.", nameof(createdByStaffUserId));
        EnsureDefined(status);
        Id = id;
        EnrollmentId = enrollmentId;
        TrainingSessionId = trainingSessionId;
        Status = status;
        CreatedByStaffUserId = createdByStaffUserId;
        LastUpdatedByStaffUserId = createdByStaffUserId;
        RecordedAtUtc = recordedAtUtc;
        LastUpdatedAtUtc = recordedAtUtc;
    }

    private AttendanceRecord() { }

    public Guid Id { get; private set; }
    public Guid EnrollmentId { get; private set; }
    public Guid TrainingSessionId { get; private set; }
    public AttendanceStatus Status { get; private set; }
    public string? CorrectionNote { get; private set; }
    public Guid CreatedByStaffUserId { get; private set; }
    public Guid LastUpdatedByStaffUserId { get; private set; }
    public DateTimeOffset RecordedAtUtc { get; private set; }
    public DateTimeOffset LastUpdatedAtUtc { get; private set; }
    public byte[]? RowVersion { get; private set; }

    public static AttendanceRecord Create(Guid enrollmentId, Guid trainingSessionId, AttendanceStatus status,
        Guid createdByStaffUserId, DateTimeOffset recordedAtUtc) =>
        new(Guid.NewGuid(), enrollmentId, trainingSessionId, status, createdByStaffUserId, recordedAtUtc);

    public bool Correct(AttendanceStatus status, string? correctionNote, Guid updatedByStaffUserId,
        DateTimeOffset updatedAtUtc)
    {
        if (updatedByStaffUserId == Guid.Empty) throw new ArgumentException("Staff user id is required.", nameof(updatedByStaffUserId));
        EnsureDefined(status);
        var normalizedNote = NormalizeNote(correctionNote);
        if (Status == status && CorrectionNote == normalizedNote) return false;
        Status = status;
        CorrectionNote = normalizedNote;
        LastUpdatedByStaffUserId = updatedByStaffUserId;
        LastUpdatedAtUtc = updatedAtUtc;
        return true;
    }

    private static string? NormalizeNote(string? note)
    {
        var normalized = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (normalized?.Length > CorrectionNoteMaxLength)
            throw new ArgumentException($"Correction note must be {CorrectionNoteMaxLength} characters or fewer.", nameof(note));
        return normalized;
    }

    private static void EnsureDefined(AttendanceStatus status)
    {
        if (!Enum.IsDefined(status)) throw new ArgumentOutOfRangeException(nameof(status));
    }
}
