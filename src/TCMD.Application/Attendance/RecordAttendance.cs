using TCMD.Domain.Attendance;
using TCMD.Domain.Enrollments;
using TCMD.Domain.TrainingSessions;

namespace TCMD.Application.Attendance;

public sealed record RecordAttendanceRequest(Guid EnrollmentId, AttendanceStatus Status);

public sealed class RecordAttendance(IAttendanceStore store, ITrainingCenterClock clock)
{
    public async Task<AttendanceMutationResult> ExecuteAsync(Guid sessionId, RecordAttendanceRequest request,
        Guid staffUserId, Guid? assignedInstructorId, CancellationToken cancellationToken)
    {
        var session = await store.GetSessionReferenceAsync(sessionId, assignedInstructorId, cancellationToken);
        if (!session.Exists) return AttendanceMutationResult.Error(AttendanceMutationStatus.SessionNotFound);
        var enrollment = await store.GetEnrollmentReferenceAsync(request.EnrollmentId, cancellationToken);
        if (!enrollment.Exists) return AttendanceMutationResult.Error(AttendanceMutationStatus.EnrollmentNotFound);
        if (enrollment.TrainingGroupId != session.TrainingGroupId)
            return AttendanceMutationResult.Error(AttendanceMutationStatus.DifferentTrainingGroup);
        if (enrollment.Status != EnrollmentStatus.Active)
            return AttendanceMutationResult.Error(AttendanceMutationStatus.EnrollmentInactive);
        if (enrollment.EnrollmentDate > session.SessionDate)
            return AttendanceMutationResult.Error(AttendanceMutationStatus.EnrollmentAfterSession);
        if (session.Status == TrainingSessionStatus.Cancelled)
            return AttendanceMutationResult.Error(AttendanceMutationStatus.SessionCancelled);
        if (session.Status == TrainingSessionStatus.Scheduled && !HasStarted(session, clock.GetLocalNow()))
            return AttendanceMutationResult.Error(AttendanceMutationStatus.SessionNotStarted);
        if (await store.AttendanceExistsAsync(request.EnrollmentId, sessionId, cancellationToken))
            return AttendanceMutationResult.Error(AttendanceMutationStatus.DuplicateAttendance);

        var attendance = AttendanceRecord.Create(request.EnrollmentId, sessionId, request.Status, staffUserId,
            clock.GetUtcNow());
        return AttendanceMutationResult.FromStore(await store.AddAsync(attendance, assignedInstructorId,
            cancellationToken), attendance);
    }

    private static bool HasStarted(AttendanceSessionReference session, DateTime localNow) =>
        DateOnly.FromDateTime(localNow) > session.SessionDate ||
        DateOnly.FromDateTime(localNow) == session.SessionDate && TimeOnly.FromDateTime(localNow) >= session.StartTime;
}
