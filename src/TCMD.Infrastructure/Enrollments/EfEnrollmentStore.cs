using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TCMD.Application.Enrollments;
using TCMD.Domain.Enrollments;
using TCMD.Infrastructure.Persistence;

namespace TCMD.Infrastructure.Enrollments;

internal sealed class EfEnrollmentStore(TcmdDbContext dbContext) : IEnrollmentStore
{
    private const string UniquePairIndex = "IX_Enrollments_StudentId_TrainingGroupId";

    public async Task<StudentEnrollmentReference> GetStudentReferenceAsync(Guid id,
        CancellationToken cancellationToken)
    {
        var active = await dbContext.Students.AsNoTracking().Where(student => student.Id == id)
            .Select(student => (bool?)student.IsActive).SingleOrDefaultAsync(cancellationToken);
        return new(active is not null, active ?? false);
    }

    public async Task<TrainingGroupEnrollmentReference> GetTrainingGroupReferenceAsync(Guid id,
        CancellationToken cancellationToken)
    {
        var status = await dbContext.TrainingGroups.AsNoTracking().Where(group => group.Id == id)
            .Select(group => (TCMD.Domain.TrainingGroups.TrainingGroupStatus?)group.Status)
            .SingleOrDefaultAsync(cancellationToken);
        return new(status is not null, status);
    }

    public Task<bool> ExistsAsync(Guid studentId, Guid trainingGroupId, CancellationToken cancellationToken) =>
        dbContext.Enrollments.AsNoTracking().AnyAsync(enrollment => enrollment.StudentId == studentId &&
            enrollment.TrainingGroupId == trainingGroupId, cancellationToken);

    public Task<Enrollment?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Enrollments.SingleOrDefaultAsync(enrollment => enrollment.Id == id, cancellationToken);

    public async Task<EnrollmentStoreSaveStatus> AddAsync(Enrollment enrollment,
        CancellationToken cancellationToken)
    {
        dbContext.Enrollments.Add(enrollment);
        return await SaveChangesAsync(enrollment, cancellationToken);
    }

    public async Task<EnrollmentStoreSaveStatus> SaveAsync(Enrollment enrollment, byte[] expectedRowVersion,
        CancellationToken cancellationToken)
    {
        dbContext.Entry(enrollment).Property(candidate => candidate.RowVersion).OriginalValue = expectedRowVersion;
        return await SaveChangesAsync(enrollment, cancellationToken);
    }

    public async Task<IReadOnlyList<GroupEnrollmentProjection>> ListByTrainingGroupAsync(Guid trainingGroupId,
        Guid? assignedInstructorId, CancellationToken cancellationToken) => await (
            from enrollment in dbContext.Enrollments.AsNoTracking()
            join student in dbContext.Students.AsNoTracking() on enrollment.StudentId equals student.Id
            join trainingGroup in dbContext.TrainingGroups.AsNoTracking()
                on enrollment.TrainingGroupId equals trainingGroup.Id
            where enrollment.TrainingGroupId == trainingGroupId &&
                (assignedInstructorId == null || trainingGroup.PrimaryInstructorId == assignedInstructorId)
            orderby student.StudentNumber, enrollment.Id
            select new GroupEnrollmentProjection(enrollment, student.StudentNumber, student.FullName, student.IsActive))
            .ToListAsync(cancellationToken);

    public Task<bool> IsTrainingGroupAssignedAsync(Guid trainingGroupId, Guid instructorId,
        CancellationToken cancellationToken) => dbContext.TrainingGroups.AsNoTracking().AnyAsync(group =>
        group.Id == trainingGroupId && group.PrimaryInstructorId == instructorId, cancellationToken);

    public async Task<IReadOnlyList<StudentEnrollmentProjection>> ListByStudentAsync(Guid studentId,
        CancellationToken cancellationToken) => await (
            from enrollment in dbContext.Enrollments.AsNoTracking()
            join trainingGroup in dbContext.TrainingGroups.AsNoTracking()
                on enrollment.TrainingGroupId equals trainingGroup.Id
            where enrollment.StudentId == studentId
            orderby trainingGroup.PlannedStartDate, trainingGroup.Name, enrollment.Id
            select new StudentEnrollmentProjection(enrollment, trainingGroup.Name, trainingGroup.Status,
                trainingGroup.CourseId, trainingGroup.PlannedStartDate, trainingGroup.PlannedEndDate))
            .ToListAsync(cancellationToken);

    private async Task<EnrollmentStoreSaveStatus> SaveChangesAsync(Enrollment enrollment,
        CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return EnrollmentStoreSaveStatus.Success;
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.Entry(enrollment).State = EntityState.Detached;
            return EnrollmentStoreSaveStatus.ConcurrencyConflict;
        }
        catch (DbUpdateException exception) when (IsUniquePairViolation(exception))
        {
            dbContext.Entry(enrollment).State = EntityState.Detached;
            return EnrollmentStoreSaveStatus.DuplicateEnrollment;
        }
    }

    private static bool IsUniquePairViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 } sqlException &&
        sqlException.Message.Contains(UniquePairIndex, StringComparison.Ordinal);
}
