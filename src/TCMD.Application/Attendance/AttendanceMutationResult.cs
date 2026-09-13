namespace TCMD.Application.Attendance;

public enum AttendanceMutationStatus
{
    Success, EnrollmentNotFound, SessionNotFound, AttendanceNotFound, DifferentTrainingGroup,
    DuplicateAttendance, EnrollmentInactive, EnrollmentAfterSession, SessionNotStarted, SessionCancelled,
    ConcurrencyConflict, AssignmentChanged
}

public sealed record AttendanceMutationResult(AttendanceMutationStatus Status, AttendanceDto? Attendance = null)
{
    public static AttendanceMutationResult Success(Domain.Attendance.AttendanceRecord attendance) =>
        new(AttendanceMutationStatus.Success, AttendanceDto.From(attendance));
    public static AttendanceMutationResult Error(AttendanceMutationStatus status) => new(status);
    public static AttendanceMutationResult FromStore(AttendanceStoreSaveStatus status,
        Domain.Attendance.AttendanceRecord attendance) => status switch
        {
            AttendanceStoreSaveStatus.Success => Success(attendance),
            AttendanceStoreSaveStatus.DuplicateAttendance => Error(AttendanceMutationStatus.DuplicateAttendance),
            AttendanceStoreSaveStatus.ConcurrencyConflict => Error(AttendanceMutationStatus.ConcurrencyConflict),
            AttendanceStoreSaveStatus.AssignmentChanged => Error(AttendanceMutationStatus.AssignmentChanged),
            _ => throw new ArgumentOutOfRangeException(nameof(status))
        };
}
