namespace TCMD.Domain.TrainingSessions;

public sealed class TrainingSession
{
    private TrainingSession() { }

    private TrainingSession(Guid id, Guid trainingGroupId, DateOnly sessionDate, TimeOnly startTime,
        TimeOnly endTime, string? location, DateTimeOffset createdAtUtc)
    {
        Id = id;
        TrainingGroupId = trainingGroupId == Guid.Empty
            ? throw new ArgumentException("A training group identifier is required.", nameof(trainingGroupId))
            : trainingGroupId;
        ValidateDetails(sessionDate, startTime, endTime);
        SessionDate = sessionDate;
        StartTime = startTime;
        EndTime = endTime;
        Location = NormalizeLocation(location);
        Status = TrainingSessionStatus.Scheduled;
        CreatedAtUtc = createdAtUtc;
        LastUpdatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid TrainingGroupId { get; private set; }
    public DateOnly SessionDate { get; private set; }
    public TimeOnly StartTime { get; private set; }
    public TimeOnly EndTime { get; private set; }
    public TrainingSessionStatus Status { get; private set; }
    public string? Location { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset LastUpdatedAtUtc { get; private set; }
    public byte[]? RowVersion { get; private set; }

    public static TrainingSession Create(Guid trainingGroupId, DateOnly sessionDate, TimeOnly startTime,
        TimeOnly endTime, string? location, DateTimeOffset createdAtUtc) =>
        new(Guid.NewGuid(), trainingGroupId, sessionDate, startTime, endTime, location, createdAtUtc);

    public bool UpdateDetails(DateOnly sessionDate, TimeOnly startTime, TimeOnly endTime, string? location,
        DateTimeOffset updatedAtUtc)
    {
        if (Status != TrainingSessionStatus.Scheduled)
            throw new InvalidOperationException("Only a Scheduled session can be updated.");
        ValidateDetails(sessionDate, startTime, endTime);
        var normalizedLocation = NormalizeLocation(location);
        if (SessionDate == sessionDate && StartTime == startTime && EndTime == endTime &&
            Location == normalizedLocation) return false;
        SessionDate = sessionDate;
        StartTime = startTime;
        EndTime = endTime;
        Location = normalizedLocation;
        LastUpdatedAtUtc = updatedAtUtc;
        return true;
    }

    public bool Complete(DateTimeOffset updatedAtUtc)
    {
        if (Status == TrainingSessionStatus.Completed) return false;
        if (Status != TrainingSessionStatus.Scheduled)
            throw new InvalidOperationException("Only a Scheduled session can be completed.");
        Status = TrainingSessionStatus.Completed;
        LastUpdatedAtUtc = updatedAtUtc;
        return true;
    }

    public bool Cancel(DateTimeOffset updatedAtUtc)
    {
        if (Status == TrainingSessionStatus.Cancelled) return false;
        if (Status != TrainingSessionStatus.Scheduled)
            throw new InvalidOperationException("Only a Scheduled session can be cancelled.");
        Status = TrainingSessionStatus.Cancelled;
        LastUpdatedAtUtc = updatedAtUtc;
        return true;
    }

    private static void ValidateDetails(DateOnly sessionDate, TimeOnly startTime, TimeOnly endTime)
    {
        if (sessionDate == default) throw new ArgumentException("A session date is required.", nameof(sessionDate));
        if (endTime <= startTime)
            throw new ArgumentException("End time must be later than start time.", nameof(endTime));
    }

    private static string? NormalizeLocation(string? location)
    {
        if (string.IsNullOrWhiteSpace(location)) return null;
        var normalized = location.Trim();
        if (normalized.Length > 500)
            throw new ArgumentException("Location must be 500 characters or fewer.", nameof(location));
        return normalized;
    }
}
