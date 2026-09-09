using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TCMD.Application.TrainingGroups;
using TCMD.Domain.TrainingGroups;
using TCMD.Domain.TrainingSessions;
using TCMD.Infrastructure.Persistence;

namespace TCMD.Infrastructure.TrainingGroups;

internal sealed class EfTrainingGroupStore(TcmdDbContext dbContext) : ITrainingGroupStore
{
    public async Task<TrainingGroupStoreSaveStatus> AddAsync(TrainingGroup group, CancellationToken cancellationToken)
    {
        dbContext.TrainingGroups.Add(group);
        return await SaveChangesAsync(group, cancellationToken);
    }

    public Task<TrainingGroup?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.TrainingGroups.AsNoTracking().SingleOrDefaultAsync(group => group.Id == id, cancellationToken);

    public Task<TrainingGroup?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.TrainingGroups.SingleOrDefaultAsync(group => group.Id == id, cancellationToken);

    public async Task<IReadOnlyList<TrainingGroup>> SearchAsync(string? search, TrainingGroupStatus? status,
        Guid? courseId, Guid? primaryInstructorId, bool? hasPrimaryInstructor, CancellationToken cancellationToken)
    {
        var query = dbContext.TrainingGroups.AsNoTracking();
        if (search is not null) query = query.Where(group => group.Name.Contains(search));
        if (status is not null) query = query.Where(group => group.Status == status);
        if (courseId is not null) query = query.Where(group => group.CourseId == courseId);
        if (primaryInstructorId is not null) query = query.Where(group => group.PrimaryInstructorId == primaryInstructorId);
        if (hasPrimaryInstructor is not null)
            query = query.Where(group => (group.PrimaryInstructorId != null) == hasPrimaryInstructor);
        return await query.OrderBy(group => group.PlannedStartDate).ThenBy(group => group.Name)
            .ThenBy(group => group.Id).ToListAsync(cancellationToken);
    }

    public async Task<TrainingGroupReference> GetCourseReferenceAsync(Guid id, CancellationToken cancellationToken)
    {
        var active = await dbContext.Courses.AsNoTracking().Where(course => course.Id == id)
            .Select(course => (bool?)course.IsActive).SingleOrDefaultAsync(cancellationToken);
        return new(active is not null, active ?? false);
    }

    public async Task<TrainingGroupReference> GetInstructorReferenceAsync(Guid id, CancellationToken cancellationToken)
    {
        var active = await dbContext.Instructors.AsNoTracking().Where(instructor => instructor.Id == id)
            .Select(instructor => (bool?)instructor.IsActive).SingleOrDefaultAsync(cancellationToken);
        return new(active is not null, active ?? false);
    }

    public Task<bool> DuplicateExistsAsync(Guid courseId, string name, DateOnly plannedStartDate, Guid? excludingId,
        CancellationToken cancellationToken) => dbContext.TrainingGroups.AsNoTracking().AnyAsync(group =>
            group.CourseId == courseId && group.Name == name && group.PlannedStartDate == plannedStartDate &&
            (excludingId == null || group.Id != excludingId), cancellationToken);

    public Task<bool> HasNonCancelledSessionsOutsideRangeAsync(Guid groupId, DateOnly startDate, DateOnly endDate,
        CancellationToken cancellationToken) => dbContext.TrainingSessions.AsNoTracking().AnyAsync(session =>
            session.TrainingGroupId == groupId && session.Status != TrainingSessionStatus.Cancelled &&
            (session.SessionDate < startDate || session.SessionDate > endDate), cancellationToken);

    public async Task<TrainingGroupStoreSaveStatus> SaveAsync(TrainingGroup group, byte[] expectedRowVersion,
        CancellationToken cancellationToken)
    {
        dbContext.Entry(group).Property(candidate => candidate.RowVersion).OriginalValue = expectedRowVersion;
        return await SaveChangesAsync(group, cancellationToken);
    }

    private async Task<TrainingGroupStoreSaveStatus> SaveChangesAsync(TrainingGroup group, CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return TrainingGroupStoreSaveStatus.Success;
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.Entry(group).State = EntityState.Detached;
            return TrainingGroupStoreSaveStatus.ConcurrencyConflict;
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            dbContext.Entry(group).State = EntityState.Detached;
            return TrainingGroupStoreSaveStatus.DuplicateGroup;
        }
    }
}
