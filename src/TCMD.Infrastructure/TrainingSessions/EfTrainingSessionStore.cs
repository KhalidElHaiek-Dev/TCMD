using Microsoft.EntityFrameworkCore;
using TCMD.Application.TrainingSessions;
using TCMD.Domain.TrainingGroups;
using TCMD.Domain.TrainingSessions;
using TCMD.Infrastructure.Persistence;

namespace TCMD.Infrastructure.TrainingSessions;

internal sealed class EfTrainingSessionStore(TcmdDbContext dbContext) : ITrainingSessionStore
{
    public async Task<TrainingSessionGroupReference> GetTrainingGroupReferenceAsync(Guid id,
        CancellationToken cancellationToken)
    {
        var row = await dbContext.TrainingGroups.AsNoTracking().Where(group => group.Id == id)
            .Select(group => new { group.Status, group.PlannedStartDate, group.PlannedEndDate })
            .SingleOrDefaultAsync(cancellationToken);
        return row is null
            ? new(false, null, default, default)
            : new(true, row.Status, row.PlannedStartDate, row.PlannedEndDate);
    }

    public Task<TrainingSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.TrainingSessions.AsNoTracking().SingleOrDefaultAsync(session => session.Id == id, cancellationToken);

    public Task<TrainingSession?> GetAssignedByIdAsync(Guid id, Guid instructorId,
        CancellationToken cancellationToken) => (from session in dbContext.TrainingSessions.AsNoTracking()
        join trainingGroup in dbContext.TrainingGroups.AsNoTracking() on session.TrainingGroupId equals trainingGroup.Id
        where session.Id == id && trainingGroup.PrimaryInstructorId == instructorId
        select session).SingleOrDefaultAsync(cancellationToken);

    public Task<TrainingSession?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.TrainingSessions.SingleOrDefaultAsync(session => session.Id == id, cancellationToken);

    public async Task<IReadOnlyList<TrainingSession>> ListByTrainingGroupAsync(Guid groupId,
        Guid? assignedInstructorId, CancellationToken cancellationToken) => await (
        from session in dbContext.TrainingSessions.AsNoTracking()
        join trainingGroup in dbContext.TrainingGroups.AsNoTracking()
            on session.TrainingGroupId equals trainingGroup.Id
        where session.TrainingGroupId == groupId &&
            (assignedInstructorId == null || trainingGroup.PrimaryInstructorId == assignedInstructorId)
        select session)
        .OrderBy(session => session.SessionDate).ThenBy(session => session.StartTime)
        .ThenBy(session => session.EndTime).ThenBy(session => session.Id).ToListAsync(cancellationToken);

    public Task<bool> IsTrainingGroupAssignedAsync(Guid groupId, Guid instructorId,
        CancellationToken cancellationToken) => dbContext.TrainingGroups.AsNoTracking().AnyAsync(group =>
        group.Id == groupId && group.PrimaryInstructorId == instructorId, cancellationToken);

    public async Task<TrainingSessionStoreSaveStatus> AddAsync(TrainingSession session,
        CancellationToken cancellationToken)
    {
        dbContext.TrainingSessions.Add(session);
        return await SaveChangesAsync(session, cancellationToken);
    }

    public async Task<TrainingSessionStoreSaveStatus> SaveAsync(TrainingSession session, byte[] expectedRowVersion,
        CancellationToken cancellationToken)
    {
        dbContext.Entry(session).Property(candidate => candidate.RowVersion).OriginalValue = expectedRowVersion;
        return await SaveChangesAsync(session, cancellationToken);
    }

    private async Task<TrainingSessionStoreSaveStatus> SaveChangesAsync(TrainingSession session,
        CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return TrainingSessionStoreSaveStatus.Success;
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.Entry(session).State = EntityState.Detached;
            return TrainingSessionStoreSaveStatus.ConcurrencyConflict;
        }
    }
}
