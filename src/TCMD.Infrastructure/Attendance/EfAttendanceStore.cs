using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TCMD.Application.Attendance;
using TCMD.Domain.Attendance;
using TCMD.Infrastructure.Persistence;
using System.Data;

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

    public async Task<AttendanceSessionReference> GetSessionReferenceAsync(Guid id, Guid? assignedInstructorId,
        CancellationToken token)
    {
        var row = await (from session in db.TrainingSessions.AsNoTracking()
            join trainingGroup in db.TrainingGroups.AsNoTracking() on session.TrainingGroupId equals trainingGroup.Id
            where session.Id == id && (assignedInstructorId == null || trainingGroup.PrimaryInstructorId == assignedInstructorId)
            select new { session.TrainingGroupId, session.SessionDate, session.StartTime, session.Status })
            .SingleOrDefaultAsync(token);
        return row is null ? new(false, default, default, default, default)
            : new(true, row.TrainingGroupId, row.SessionDate, row.StartTime, row.Status);
    }

    public Task<bool> AttendanceExistsAsync(Guid enrollmentId, Guid sessionId, CancellationToken token) =>
        db.AttendanceRecords.AsNoTracking().AnyAsync(x => x.EnrollmentId == enrollmentId && x.TrainingSessionId == sessionId, token);
    public Task<AttendanceRecord?> GetForUpdateAsync(Guid id, Guid? assignedInstructorId, CancellationToken token) =>
        (from attendance in db.AttendanceRecords
         join session in db.TrainingSessions on attendance.TrainingSessionId equals session.Id
         join trainingGroup in db.TrainingGroups on session.TrainingGroupId equals trainingGroup.Id
         where attendance.Id == id && (assignedInstructorId == null || trainingGroup.PrimaryInstructorId == assignedInstructorId)
         select attendance).SingleOrDefaultAsync(token);

    public Task<AttendanceStoreSaveStatus> AddAsync(AttendanceRecord attendance, Guid? assignedInstructorId,
        CancellationToken token) => SaveAuthorizedAsync(attendance, null, assignedInstructorId, token);
    public Task<AttendanceStoreSaveStatus> SaveAsync(AttendanceRecord attendance, byte[] expected,
        Guid? assignedInstructorId, CancellationToken token) =>
        SaveAuthorizedAsync(attendance, expected, assignedInstructorId, token);

    public async Task<IReadOnlyList<SessionAttendanceProjection>> ListBySessionAsync(Guid sessionId,
        Guid? assignedInstructorId, CancellationToken token)
    {
        var session = await db.TrainingSessions.AsNoTracking().SingleAsync(x => x.Id == sessionId, token);
        return await (from enrollment in db.Enrollments.AsNoTracking()
            join student in db.Students.AsNoTracking() on enrollment.StudentId equals student.Id
            join trainingGroup in db.TrainingGroups.AsNoTracking() on enrollment.TrainingGroupId equals trainingGroup.Id
            join attendance in db.AttendanceRecords.AsNoTracking().Where(x => x.TrainingSessionId == sessionId)
                on enrollment.Id equals attendance.EnrollmentId into matches
            from attendance in matches.DefaultIfEmpty()
            where enrollment.TrainingGroupId == session.TrainingGroupId &&
                (assignedInstructorId == null || trainingGroup.PrimaryInstructorId == assignedInstructorId) &&
                ((enrollment.Status == Domain.Enrollments.EnrollmentStatus.Active && enrollment.EnrollmentDate <= session.SessionDate) || attendance != null)
            orderby student.StudentNumber, student.Id
            select new SessionAttendanceProjection(new(true, enrollment.Id, enrollment.StudentId,
                enrollment.TrainingGroupId, enrollment.EnrollmentDate, enrollment.Status), student.StudentNumber,
                student.FullName, student.IsActive, attendance)).ToListAsync(token);
    }

    public Task<bool> StudentExistsAsync(Guid id, Guid? assignedInstructorId, CancellationToken token) =>
        assignedInstructorId is null
            ? db.Students.AsNoTracking().AnyAsync(x => x.Id == id, token)
            : (from enrollment in db.Enrollments.AsNoTracking()
               join trainingGroup in db.TrainingGroups.AsNoTracking() on enrollment.TrainingGroupId equals trainingGroup.Id
               where enrollment.StudentId == id && trainingGroup.PrimaryInstructorId == assignedInstructorId
               select enrollment.Id).AnyAsync(token);
    public async Task<IReadOnlyList<StudentAttendanceProjection>> ListByStudentAsync(Guid id,
        Guid? assignedInstructorId, CancellationToken token) => await (
        from attendance in db.AttendanceRecords.AsNoTracking()
        join enrollment in db.Enrollments.AsNoTracking() on attendance.EnrollmentId equals enrollment.Id
        join session in db.TrainingSessions.AsNoTracking() on attendance.TrainingSessionId equals session.Id
        join trainingGroup in db.TrainingGroups.AsNoTracking() on enrollment.TrainingGroupId equals trainingGroup.Id
        where enrollment.StudentId == id &&
            (assignedInstructorId == null || trainingGroup.PrimaryInstructorId == assignedInstructorId)
        orderby session.SessionDate, session.StartTime, session.Id
        select new StudentAttendanceProjection(attendance, enrollment.Status, trainingGroup.Id, trainingGroup.Name, session.SessionDate,
            session.StartTime, session.EndTime, session.Status)).ToListAsync(token);

    public Task<bool> TrainingGroupExistsAsync(Guid id, Guid? assignedInstructorId, CancellationToken token) =>
        db.TrainingGroups.AsNoTracking().AnyAsync(x => x.Id == id &&
            (assignedInstructorId == null || x.PrimaryInstructorId == assignedInstructorId), token);
    public async Task<IReadOnlyList<GroupAttendanceProjection>> ListByTrainingGroupAsync(Guid id,
        Guid? assignedInstructorId, CancellationToken token) => await (
        from attendance in db.AttendanceRecords.AsNoTracking()
        join enrollment in db.Enrollments.AsNoTracking() on attendance.EnrollmentId equals enrollment.Id
        join student in db.Students.AsNoTracking() on enrollment.StudentId equals student.Id
        join session in db.TrainingSessions.AsNoTracking() on attendance.TrainingSessionId equals session.Id
        join trainingGroup in db.TrainingGroups.AsNoTracking() on enrollment.TrainingGroupId equals trainingGroup.Id
        where enrollment.TrainingGroupId == id && session.TrainingGroupId == id &&
            (assignedInstructorId == null || trainingGroup.PrimaryInstructorId == assignedInstructorId)
        orderby session.SessionDate, session.StartTime, student.StudentNumber, student.Id
        select new GroupAttendanceProjection(attendance, enrollment.Status, student.Id, student.StudentNumber,
            student.FullName, student.IsActive, session.SessionDate, session.StartTime, session.EndTime,
            session.Status)).ToListAsync(token);

    private async Task<AttendanceStoreSaveStatus> SaveAuthorizedAsync(AttendanceRecord attendance, byte[]? expected,
        Guid? assignedInstructorId, CancellationToken token)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
            if (assignedInstructorId is not null && !await (from session in db.TrainingSessions.AsNoTracking()
                    join trainingGroup in db.TrainingGroups.AsNoTracking() on session.TrainingGroupId equals trainingGroup.Id
                    where session.Id == attendance.TrainingSessionId && trainingGroup.PrimaryInstructorId == assignedInstructorId
                    select session.Id).AnyAsync(token))
                return AttendanceStoreSaveStatus.AssignmentChanged;
            if (expected is null) db.AttendanceRecords.Add(attendance);
            else db.Entry(attendance).Property(x => x.RowVersion).OriginalValue = expected;
            var result = await SaveChangesAsync(attendance, token);
            if (result == AttendanceStoreSaveStatus.Success) await transaction.CommitAsync(token);
            return result;
        });
    }

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
