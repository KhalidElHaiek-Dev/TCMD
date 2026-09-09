using TCMD.Domain.Attendance;
using TCMD.Domain.Enrollments;
using TCMD.Domain.TrainingSessions;

namespace TCMD.Application.Attendance;

public sealed record AttendanceDto(Guid Id, Guid EnrollmentId, Guid TrainingSessionId, AttendanceStatus Status,
    string? CorrectionNote, Guid CreatedByStaffUserId, Guid LastUpdatedByStaffUserId,
    DateTimeOffset RecordedAtUtc, DateTimeOffset LastUpdatedAtUtc, byte[] RowVersion)
{
    public static AttendanceDto From(AttendanceRecord attendance) => new(attendance.Id, attendance.EnrollmentId,
        attendance.TrainingSessionId, attendance.Status, attendance.CorrectionNote, attendance.CreatedByStaffUserId,
        attendance.LastUpdatedByStaffUserId, attendance.RecordedAtUtc, attendance.LastUpdatedAtUtc,
        attendance.RowVersion ?? []);
}

public sealed record AttendanceStudentDto(Guid Id, string StudentNumber, string FullName, bool IsActive);
public sealed record SessionAttendanceRosterDto(Guid EnrollmentId, EnrollmentStatus EnrollmentStatus,
    AttendanceStudentDto Student, AttendanceDto? Attendance);
public sealed record AttendanceSessionDto(Guid Id, Guid TrainingGroupId, string TrainingGroupName,
    DateOnly SessionDate, TimeOnly StartTime, TimeOnly EndTime, TrainingSessionStatus Status);
public sealed record StudentAttendanceDto(AttendanceDto Attendance, EnrollmentStatus EnrollmentStatus,
    AttendanceSessionDto Session);
public sealed record GroupAttendanceDto(AttendanceDto Attendance, EnrollmentStatus EnrollmentStatus,
    AttendanceStudentDto Student, DateOnly SessionDate, TimeOnly StartTime, TimeOnly EndTime,
    TrainingSessionStatus SessionStatus);
