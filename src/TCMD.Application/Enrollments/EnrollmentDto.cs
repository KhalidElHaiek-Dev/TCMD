using TCMD.Domain.Enrollments;
using TCMD.Domain.TrainingGroups;

namespace TCMD.Application.Enrollments;

public sealed record EnrollmentDto(Guid Id, Guid StudentId, Guid TrainingGroupId, DateOnly EnrollmentDate,
    EnrollmentStatus Status, DateTimeOffset CreatedAtUtc, DateTimeOffset LastUpdatedAtUtc, byte[] RowVersion)
{
    public static EnrollmentDto From(Enrollment enrollment) => new(enrollment.Id, enrollment.StudentId,
        enrollment.TrainingGroupId, enrollment.EnrollmentDate, enrollment.Status, enrollment.CreatedAtUtc,
        enrollment.LastUpdatedAtUtc, enrollment.RowVersion ?? []);
}

public sealed record CompactStudentDto(Guid Id, string StudentNumber, string FullName, bool IsActive);
public sealed record TrainingGroupEnrollmentDto(Guid Id, Guid StudentId, Guid TrainingGroupId,
    DateOnly EnrollmentDate, EnrollmentStatus Status, DateTimeOffset CreatedAtUtc,
    DateTimeOffset LastUpdatedAtUtc, byte[] RowVersion, CompactStudentDto Student)
{
    public static TrainingGroupEnrollmentDto From(GroupEnrollmentProjection projection)
    {
        var enrollment = projection.Enrollment;
        return new(enrollment.Id, enrollment.StudentId, enrollment.TrainingGroupId, enrollment.EnrollmentDate,
            enrollment.Status, enrollment.CreatedAtUtc, enrollment.LastUpdatedAtUtc, enrollment.RowVersion ?? [],
            new(enrollment.StudentId, projection.StudentNumber, projection.StudentFullName, projection.StudentIsActive));
    }
}

public sealed record CompactTrainingGroupDto(Guid Id, string Name, TrainingGroupStatus Status, Guid CourseId,
    DateOnly PlannedStartDate, DateOnly PlannedEndDate);
public sealed record StudentEnrollmentDto(Guid Id, Guid StudentId, Guid TrainingGroupId,
    DateOnly EnrollmentDate, EnrollmentStatus Status, DateTimeOffset CreatedAtUtc,
    DateTimeOffset LastUpdatedAtUtc, byte[] RowVersion, CompactTrainingGroupDto TrainingGroup)
{
    public static StudentEnrollmentDto From(StudentEnrollmentProjection projection)
    {
        var enrollment = projection.Enrollment;
        return new(enrollment.Id, enrollment.StudentId, enrollment.TrainingGroupId, enrollment.EnrollmentDate,
            enrollment.Status, enrollment.CreatedAtUtc, enrollment.LastUpdatedAtUtc, enrollment.RowVersion ?? [],
            new(enrollment.TrainingGroupId, projection.GroupName, projection.GroupStatus, projection.CourseId,
                projection.PlannedStartDate, projection.PlannedEndDate));
    }
}
