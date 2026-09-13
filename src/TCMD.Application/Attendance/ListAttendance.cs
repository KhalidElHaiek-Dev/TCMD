namespace TCMD.Application.Attendance;

public sealed record SessionAttendanceListResult(bool Found, IReadOnlyList<SessionAttendanceRosterDto> Items);
public sealed record StudentAttendanceListResult(bool Found, IReadOnlyList<StudentAttendanceDto> Items);
public sealed record GroupAttendanceListResult(bool Found, IReadOnlyList<GroupAttendanceDto> Items);

public sealed class ListTrainingSessionAttendance(IAttendanceStore store)
{
    public async Task<SessionAttendanceListResult> ExecuteAsync(Guid id, Guid? assignedInstructorId,
        CancellationToken token)
    {
        if (!(await store.GetSessionReferenceAsync(id, assignedInstructorId, token)).Exists) return new(false, []);
        var rows = await store.ListBySessionAsync(id, assignedInstructorId, token);
        return new(true, rows.Select(row => new SessionAttendanceRosterDto(row.Enrollment.EnrollmentId,
            row.Enrollment.Status, new(row.Enrollment.StudentId, row.StudentNumber, row.StudentFullName,
                row.StudentIsActive), row.Attendance is null ? null : AttendanceDto.From(row.Attendance))).ToList());
    }
}

public sealed class ListStudentAttendance(IAttendanceStore store)
{
    public async Task<StudentAttendanceListResult> ExecuteAsync(Guid id, Guid? assignedInstructorId,
        CancellationToken token)
    {
        if (!await store.StudentExistsAsync(id, assignedInstructorId, token)) return new(false, []);
        var rows = await store.ListByStudentAsync(id, assignedInstructorId, token);
        return new(true, rows.Select(row => new StudentAttendanceDto(AttendanceDto.From(row.Attendance),
            row.EnrollmentStatus, new(row.Attendance.TrainingSessionId, row.TrainingGroupId, row.TrainingGroupName,
                row.SessionDate, row.StartTime, row.EndTime, row.SessionStatus))).ToList());
    }
}

public sealed class ListTrainingGroupAttendance(IAttendanceStore store)
{
    public async Task<GroupAttendanceListResult> ExecuteAsync(Guid id, Guid? assignedInstructorId,
        CancellationToken token)
    {
        if (!await store.TrainingGroupExistsAsync(id, assignedInstructorId, token)) return new(false, []);
        var rows = await store.ListByTrainingGroupAsync(id, assignedInstructorId, token);
        return new(true, rows.Select(row => new GroupAttendanceDto(AttendanceDto.From(row.Attendance),
            row.EnrollmentStatus, new(row.StudentId, row.StudentNumber, row.StudentFullName, row.StudentIsActive),
            row.SessionDate, row.StartTime, row.EndTime, row.SessionStatus)).ToList());
    }
}
