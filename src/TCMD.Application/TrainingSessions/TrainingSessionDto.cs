using TCMD.Domain.TrainingSessions;

namespace TCMD.Application.TrainingSessions;

public sealed record TrainingSessionDto(Guid Id, Guid TrainingGroupId, DateOnly SessionDate,
    TimeOnly StartTime, TimeOnly EndTime, TrainingSessionStatus Status, string? Location,
    DateTimeOffset CreatedAtUtc, DateTimeOffset LastUpdatedAtUtc, byte[] RowVersion)
{
    public static TrainingSessionDto From(TrainingSession session) => new(session.Id, session.TrainingGroupId,
        session.SessionDate, session.StartTime, session.EndTime, session.Status, session.Location,
        session.CreatedAtUtc, session.LastUpdatedAtUtc, session.RowVersion ?? []);
}
