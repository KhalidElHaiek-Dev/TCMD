using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TCMD.Application.Attendance;
using TCMD.Domain.Attendance;
using TCMD.Infrastructure.Persistence;

namespace TCMD.Infrastructure.Attendance;

internal sealed class EfAttendanceStore(TcmdDbContext db) : IAttendanceStore
{
    private const string UniquePairIndex = "IX_AttendanceRecords_EnrollmentId_TrainingSessionId";

    public async Task<AttendanceEnrollmentReference> GetEnrollmentReferenceAsync(Guid id, CancellationToken token)
    {
        var row = await db.Enrollments.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new { x.Id, x.StudentId, x.TrainingGroupId, x.EnrollmentDate, x.Status }).SingleOrDefaultAsync(token);
        return row is null ? new(false, default, default, default, default, default)
            : new(true, row.Id, row.StudentId, row.TrainingGroupId, row.EnrollmentDate, row.Status);
    }

    public async Task<AttendanceSessionReference> GetSessionReferenceAsync(Guid id, CancellationToken token)
    {
        var row = await db.TrainingSessions.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new { x.TrainingGroupId, x.SessionDate, x.StartTime, x.Status }).SingleOrDefaultAsync(token);
        return row is null ? new(false, default, default, default, default)
            : new(true, row.TrainingGroupId, row.SessionDate, row.StartTime, row.Status);
    }

    public Task<bool> AttendanceExistsAsync(Guid enrollmentId, Guid sessionId, CancellationToken token) =>
        db.AttendanceRecords.AsNoTracking().AnyAsync(x => x.EnrollmentId == enrollmentId && x.TrainingSessionId == sessionId, token);
    public Task<AttendanceRecord?> GetForUpdateAsync(Guid id, CancellationToken token) =>
        db.AttendanceRecords.SingleOrDefaultAsync(x => x.Id == id, token);

    public async Task<AttendanceStoreSaveStatus> AddAsync(AttendanceRecord attendance, CancellationToken token)
    { db.AttendanceRecords.Add(attendance); return await SaveChangesAsync(attendance, token); }
    public async Task<AttendanceStoreSaveStatus> SaveAsync(AttendanceRecord attendance, byte[] expected, CancellationToken token)
    { db.Entry(attendance).Property(x => x.RowVersion).OriginalValue = expected; return await SaveChangesAsync(attendance, token); }

    public async Task<IReadOnlyList<SessionAttendanceProjection>> ListBySessionAsync(Guid sessionId, CancellationToken token)
    {
        var session = await db.TrainingSessions.AsNoTracking().SingleAsync(x => x.Id == sessionId, token);
        return await (from enrollment in db.Enrollments.AsNoTracking()
            join student in db.Students.AsNoTracking() on enrollment.StudentId equals student.Id
            join attendance in db.AttendanceRecords.AsNoTracking().Where(x => x.TrainingSessionId == sessionId)
                on enrollment.Id equals attendance.EnrollmentId into matches
            from attendance in matches.DefaultIfEmpty()
            where enrollment.TrainingGroupId == session.TrainingGroupId &&
                ((enrollment.Status == Domain.Enrollments.EnrollmentStatus.Active && enrollment.EnrollmentDate <= session.SessionDate) || attendance != null)
            orderby student.StudentNumber, student.Id
            select new SessionAttendanceProjection(new(true, enrollment.Id, enrollment.StudentId,
                enrollment.TrainingGroupId, enrollment.EnrollmentDate, enrollment.Status), student.StudentNumber,
                student.FullName, student.IsActive, attendance)).ToListAsync(token);
    }

    public Task<bool> StudentExistsAsync(Guid id, CancellationToken token) => db.Students.AsNoTracking().AnyAsync(x => x.Id == id, token);
    public async Task<IReadOnlyList<StudentAttendanceProjection>> ListByStudentAsync(Guid id, CancellationToken token) => await (
        from attendance in db.AttendanceRecords.AsNoTracking()
        join enrollment in db.Enrollments.AsNoTracking() on attendance.EnrollmentId equals enrollment.Id
        join session in db.TrainingSessions.AsNoTracking() on attendance.TrainingSessionId equals session.Id
        join trainingGroup in db.TrainingGroups.AsNoTracking() on enrollment.TrainingGroupId equals trainingGroup.Id
        where enrollment.StudentId == id
        orderby session.SessionDate, session.StartTime, session.Id
        select new StudentAttendanceProjection(attendance, enrollment.Status, trainingGroup.Id, trainingGroup.Name, session.SessionDate,
            session.StartTime, session.EndTime, session.Status)).ToListAsync(token);

    public Task<bool> TrainingGroupExistsAsync(Guid id, CancellationToken token) => db.TrainingGroups.AsNoTracking().AnyAsync(x => x.Id == id, token);
    public async Task<IReadOnlyList<GroupAttendanceProjection>> ListByTrainingGroupAsync(Guid id, CancellationToken token) => await (
        from attendance in db.AttendanceRecords.AsNoTracking()
        join enrollment in db.Enrollments.AsNoTracking() on attendance.EnrollmentId equals enrollment.Id
        join student in db.Students.AsNoTracking() on enrollment.StudentId equals student.Id
        join session in db.TrainingSessions.AsNoTracking() on attendance.TrainingSessionId equals session.Id
        where enrollment.TrainingGroupId == id && session.TrainingGroupId == id
        orderby session.SessionDate, session.StartTime, student.StudentNumber, student.Id
        select new GroupAttendanceProjection(attendance, enrollment.Status, student.Id, student.StudentNumber,
            student.FullName, student.IsActive, session.SessionDate, session.StartTime, session.EndTime,
            session.Status)).ToListAsync(token);

    private async Task<AttendanceStoreSaveStatus> SaveChangesAsync(AttendanceRecord attendance, CancellationToken token)
    {
        try { await db.SaveChangesAsync(token); return AttendanceStoreSaveStatus.Success; }
        catch (DbUpdateConcurrencyException) { db.Entry(attendance).State = EntityState.Detached; return AttendanceStoreSaveStatus.ConcurrencyConflict; }
        catch (DbUpdateException exception) when (IsUniquePairViolation(exception))
        { db.Entry(attendance).State = EntityState.Detached; return AttendanceStoreSaveStatus.DuplicateAttendance; }
    }
    private static bool IsUniquePairViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 } sql && sql.Message.Contains(UniquePairIndex, StringComparison.Ordinal);
}
