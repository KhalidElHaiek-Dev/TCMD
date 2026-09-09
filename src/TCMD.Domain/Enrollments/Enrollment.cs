namespace TCMD.Domain.Enrollments;

public sealed class Enrollment
{
    private Enrollment() { }

    private Enrollment(Guid id, Guid studentId, Guid trainingGroupId, DateOnly enrollmentDate,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        StudentId = RequireId(studentId, nameof(studentId));
        TrainingGroupId = RequireId(trainingGroupId, nameof(trainingGroupId));
        if (enrollmentDate == default)
            throw new ArgumentException("An enrollment date is required.", nameof(enrollmentDate));
        EnrollmentDate = enrollmentDate;
        Status = EnrollmentStatus.Active;
        CreatedAtUtc = createdAtUtc;
        LastUpdatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid StudentId { get; private set; }
    public Guid TrainingGroupId { get; private set; }
    public DateOnly EnrollmentDate { get; private set; }
    public EnrollmentStatus Status { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset LastUpdatedAtUtc { get; private set; }
    public byte[]? RowVersion { get; private set; }

    public static Enrollment Create(Guid studentId, Guid trainingGroupId, DateOnly enrollmentDate,
        DateTimeOffset createdAtUtc) =>
        new(Guid.NewGuid(), studentId, trainingGroupId, enrollmentDate, createdAtUtc);

    public bool Complete(DateTimeOffset updatedAtUtc)
    {
        if (Status == EnrollmentStatus.Completed) return false;
        if (Status != EnrollmentStatus.Active)
            throw new InvalidOperationException("Only an Active enrollment can be completed.");
        return ChangeStatus(EnrollmentStatus.Completed, updatedAtUtc);
    }

    public bool Withdraw(DateTimeOffset updatedAtUtc)
    {
        if (Status == EnrollmentStatus.Withdrawn) return false;
        if (Status != EnrollmentStatus.Active)
            throw new InvalidOperationException("Only an Active enrollment can be withdrawn.");
        return ChangeStatus(EnrollmentStatus.Withdrawn, updatedAtUtc);
    }

    public bool Reactivate(DateTimeOffset updatedAtUtc)
    {
        if (Status == EnrollmentStatus.Active) return false;
        if (Status != EnrollmentStatus.Withdrawn)
            throw new InvalidOperationException("Only a Withdrawn enrollment can be reactivated.");
        return ChangeStatus(EnrollmentStatus.Active, updatedAtUtc);
    }

    private bool ChangeStatus(EnrollmentStatus status, DateTimeOffset updatedAtUtc)
    {
        Status = status;
        LastUpdatedAtUtc = updatedAtUtc;
        return true;
    }

    private static Guid RequireId(Guid value, string parameterName) =>
        value == Guid.Empty ? throw new ArgumentException("An identifier is required.", parameterName) : value;
}
