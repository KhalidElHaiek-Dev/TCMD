using TCMD.Domain.Enrollments;
using TCMD.Domain.TrainingGroups;

namespace TCMD.Application.Enrollments;

public enum EnrollmentStoreSaveStatus { Success, ConcurrencyConflict, DuplicateEnrollment }
public sealed record StudentEnrollmentReference(bool Exists, bool IsActive);
public sealed record TrainingGroupEnrollmentReference(bool Exists, TrainingGroupStatus? Status);
public sealed record GroupEnrollmentProjection(Enrollment Enrollment, string StudentNumber, string StudentFullName,
    bool StudentIsActive);
public sealed record StudentEnrollmentProjection(Enrollment Enrollment, string GroupName, TrainingGroupStatus GroupStatus,
    Guid CourseId, DateOnly PlannedStartDate, DateOnly PlannedEndDate);

public interface IEnrollmentStore
{
    Task<StudentEnrollmentReference> GetStudentReferenceAsync(Guid id, CancellationToken cancellationToken);
    Task<TrainingGroupEnrollmentReference> GetTrainingGroupReferenceAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> ExistsAsync(Guid studentId, Guid trainingGroupId, CancellationToken cancellationToken);
    Task<Enrollment?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken);
    Task<EnrollmentStoreSaveStatus> AddAsync(Enrollment enrollment, CancellationToken cancellationToken);
    Task<EnrollmentStoreSaveStatus> SaveAsync(Enrollment enrollment, byte[] expectedRowVersion,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<GroupEnrollmentProjection>> ListByTrainingGroupAsync(Guid trainingGroupId,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<StudentEnrollmentProjection>> ListByStudentAsync(Guid studentId,
        CancellationToken cancellationToken);
}
