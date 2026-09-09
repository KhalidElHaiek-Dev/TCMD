using TCMD.Domain.Attendance;

namespace TCMD.Application.Attendance;

public sealed record CorrectAttendanceRequest(AttendanceStatus Status, string? CorrectionNote, byte[] RowVersion);

public sealed class CorrectAttendance(IAttendanceStore store, ITrainingCenterClock clock)
{
    public async Task<AttendanceMutationResult> ExecuteAsync(Guid id, CorrectAttendanceRequest request,
        Guid staffUserId, CancellationToken cancellationToken)
    {
        var attendance = await store.GetForUpdateAsync(id, cancellationToken);
        if (attendance is null) return AttendanceMutationResult.Error(AttendanceMutationStatus.AttendanceNotFound);
        if (attendance.RowVersion is null || !attendance.RowVersion.SequenceEqual(request.RowVersion))
            return AttendanceMutationResult.Error(AttendanceMutationStatus.ConcurrencyConflict);
        var changed = attendance.Correct(request.Status, request.CorrectionNote, staffUserId, clock.GetUtcNow());
        return changed
            ? AttendanceMutationResult.FromStore(await store.SaveAsync(attendance, request.RowVersion, cancellationToken), attendance)
            : AttendanceMutationResult.Success(attendance);
    }
}
