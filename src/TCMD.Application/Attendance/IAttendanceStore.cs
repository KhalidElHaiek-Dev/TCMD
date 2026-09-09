using TCMD.Domain.Attendance;
using TCMD.Domain.Enrollments;
using TCMD.Domain.TrainingSessions;

namespace TCMD.Application.Attendance;

public enum AttendanceStoreSaveStatus { Success, DuplicateAttendance, ConcurrencyConflict }
public sealed record AttendanceEnrollmentReference(bool Exists, Guid EnrollmentId, Guid StudentId, Guid TrainingGroupId,
    DateOnly EnrollmentDate, EnrollmentStatus Status);
public sealed record AttendanceSessionReference(bool Exists, Guid TrainingGroupId, DateOnly SessionDate,
    TimeOnly StartTime, TrainingSessionStatus Status);

public sealed record SessionAttendanceProjection(AttendanceEnrollmentReference Enrollment, string StudentNumber,
    string StudentFullName, bool StudentIsActive, AttendanceRecord? Attendance);
public sealed record StudentAttendanceProjection(AttendanceRecord Attendance, EnrollmentStatus EnrollmentStatus,
    Guid TrainingGroupId, string TrainingGroupName, DateOnly SessionDate, TimeOnly StartTime, TimeOnly EndTime,
    TrainingSessionStatus SessionStatus);
public sealed record GroupAttendanceProjection(AttendanceRecord Attendance, EnrollmentStatus EnrollmentStatus,
    Guid StudentId, string StudentNumber, string StudentFullName, bool StudentIsActive,
    DateOnly SessionDate, TimeOnly StartTime, TimeOnly EndTime, TrainingSessionStatus SessionStatus);

public interface IAttendanceStore
{
    Task<AttendanceEnrollmentReference> GetEnrollmentReferenceAsync(Guid id, CancellationToken cancellationToken);
    Task<AttendanceSessionReference> GetSessionReferenceAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> AttendanceExistsAsync(Guid enrollmentId, Guid sessionId, CancellationToken cancellationToken);
    Task<AttendanceRecord?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken);
    Task<AttendanceStoreSaveStatus> AddAsync(AttendanceRecord attendance, CancellationToken cancellationToken);
    Task<AttendanceStoreSaveStatus> SaveAsync(AttendanceRecord attendance, byte[] expectedRowVersion,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<SessionAttendanceProjection>> ListBySessionAsync(Guid sessionId,
        CancellationToken cancellationToken);
    Task<bool> StudentExistsAsync(Guid studentId, CancellationToken cancellationToken);
    Task<IReadOnlyList<StudentAttendanceProjection>> ListByStudentAsync(Guid studentId,
        CancellationToken cancellationToken);
    Task<bool> TrainingGroupExistsAsync(Guid groupId, CancellationToken cancellationToken);
    Task<IReadOnlyList<GroupAttendanceProjection>> ListByTrainingGroupAsync(Guid groupId,
        CancellationToken cancellationToken);
}
