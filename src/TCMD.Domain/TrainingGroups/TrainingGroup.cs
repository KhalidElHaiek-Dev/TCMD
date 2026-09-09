namespace TCMD.Domain.TrainingGroups;

public sealed class TrainingGroup
{
    private TrainingGroup() { }

    private TrainingGroup(Guid id, string name, Guid courseId, Guid? primaryInstructorId,
        DateOnly plannedStartDate, DateOnly plannedEndDate, DateTimeOffset createdAtUtc)
    {
        Id = id;
        Name = NormalizeName(name);
        CourseId = RequireId(courseId, nameof(courseId));
        PrimaryInstructorId = NormalizeOptionalId(primaryInstructorId, nameof(primaryInstructorId));
        ValidateDates(plannedStartDate, plannedEndDate);
        PlannedStartDate = plannedStartDate;
        PlannedEndDate = plannedEndDate;
        Status = TrainingGroupStatus.Planned;
        CreatedAtUtc = createdAtUtc;
        LastUpdatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public Guid CourseId { get; private set; }
    public Guid? PrimaryInstructorId { get; private set; }
    public DateOnly PlannedStartDate { get; private set; }
    public DateOnly PlannedEndDate { get; private set; }
    public TrainingGroupStatus Status { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset LastUpdatedAtUtc { get; private set; }
    public byte[]? RowVersion { get; private set; }

    public static TrainingGroup Create(string name, Guid courseId, Guid? primaryInstructorId,
        DateOnly plannedStartDate, DateOnly plannedEndDate, DateTimeOffset createdAtUtc) =>
        new(Guid.NewGuid(), name, courseId, primaryInstructorId, plannedStartDate, plannedEndDate, createdAtUtc);

    public bool UpdateDetails(string name, DateOnly plannedStartDate, DateOnly plannedEndDate,
        DateTimeOffset updatedAtUtc)
    {
        EnsureDetailsEditable();
        var normalizedName = NormalizeName(name);
        ValidateDates(plannedStartDate, plannedEndDate);
        if (Name == normalizedName && PlannedStartDate == plannedStartDate && PlannedEndDate == plannedEndDate)
            return false;
        Name = normalizedName;
        PlannedStartDate = plannedStartDate;
        PlannedEndDate = plannedEndDate;
        LastUpdatedAtUtc = updatedAtUtc;
        return true;
    }

    public bool ChangeCourse(Guid courseId, DateTimeOffset updatedAtUtc)
    {
        courseId = RequireId(courseId, nameof(courseId));
        if (CourseId == courseId) return false;
        if (Status != TrainingGroupStatus.Planned)
            throw new InvalidOperationException("The course can only be changed while the group is Planned.");
        CourseId = courseId;
        LastUpdatedAtUtc = updatedAtUtc;
        return true;
    }

    public bool ChangePrimaryInstructor(Guid? primaryInstructorId, DateTimeOffset updatedAtUtc)
    {
        if (Status is TrainingGroupStatus.Completed or TrainingGroupStatus.Cancelled)
            throw new InvalidOperationException("The instructor cannot be changed for a terminal group.");
        primaryInstructorId = NormalizeOptionalId(primaryInstructorId, nameof(primaryInstructorId));
        if (Status == TrainingGroupStatus.Active && primaryInstructorId is null)
            throw new InvalidOperationException("An Active group must have a primary instructor.");
        if (PrimaryInstructorId == primaryInstructorId) return false;
        PrimaryInstructorId = primaryInstructorId;
        LastUpdatedAtUtc = updatedAtUtc;
        return true;
    }

    public bool Activate(DateTimeOffset updatedAtUtc)
    {
        if (Status != TrainingGroupStatus.Planned)
            throw new InvalidOperationException("Only a Planned group can be activated.");
        if (PrimaryInstructorId is null)
            throw new InvalidOperationException("A primary instructor is required before activation.");
        Status = TrainingGroupStatus.Active;
        LastUpdatedAtUtc = updatedAtUtc;
        return true;
    }

    public bool Complete(DateTimeOffset updatedAtUtc)
    {
        if (Status == TrainingGroupStatus.Completed) return false;
        if (Status != TrainingGroupStatus.Active)
            throw new InvalidOperationException("Only an Active group can be completed.");
        Status = TrainingGroupStatus.Completed;
        LastUpdatedAtUtc = updatedAtUtc;
        return true;
    }

    public bool Cancel(DateTimeOffset updatedAtUtc)
    {
        if (Status == TrainingGroupStatus.Cancelled) return false;
        if (Status is not (TrainingGroupStatus.Planned or TrainingGroupStatus.Active))
            throw new InvalidOperationException("Only a Planned or Active group can be cancelled.");
        Status = TrainingGroupStatus.Cancelled;
        LastUpdatedAtUtc = updatedAtUtc;
        return true;
    }

    private void EnsureDetailsEditable()
    {
        if (Status is TrainingGroupStatus.Completed or TrainingGroupStatus.Cancelled)
            throw new InvalidOperationException("Terminal group details cannot be changed.");
    }

    private static string NormalizeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A name is required.", nameof(value));
        var normalized = value.Trim();
        if (normalized.Length > 200)
            throw new ArgumentException("Name must be 200 characters or fewer.", nameof(value));
        return normalized;
    }

    private static Guid RequireId(Guid value, string parameterName) =>
        value == Guid.Empty ? throw new ArgumentException("An identifier is required.", parameterName) : value;

    private static Guid? NormalizeOptionalId(Guid? value, string parameterName) =>
        value == Guid.Empty ? throw new ArgumentException("An identifier cannot be empty.", parameterName) : value;

    private static void ValidateDates(DateOnly start, DateOnly end)
    {
        if (end < start) throw new ArgumentException("Planned end date cannot be before planned start date.", nameof(end));
    }
}
