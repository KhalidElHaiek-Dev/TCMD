using System.Security.Claims;
using TCMD.Api.Authentication;
using TCMD.Application.Attendance;
using TCMD.Domain.Attendance;

namespace TCMD.Api.Attendance;

public static class AttendanceEndpoints
{
    public static IEndpointRouteBuilder MapAttendanceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/training-sessions/{sessionId:guid}/attendance", RecordAsync).WithTags("Attendance").RequireAuthorization(TcmdPolicies.OperationalStaffOrInstructor);
        endpoints.MapGet("/api/training-sessions/{sessionId:guid}/attendance", ListSessionAsync).WithTags("Attendance").RequireAuthorization(TcmdPolicies.OperationalStaffOrInstructor);
        endpoints.MapGet("/api/students/{studentId:guid}/attendance", ListStudentAsync).WithTags("Attendance").RequireAuthorization(TcmdPolicies.OperationalStaffOrInstructor);
        endpoints.MapGet("/api/training-groups/{groupId:guid}/attendance", ListGroupAsync).WithTags("Attendance").RequireAuthorization(TcmdPolicies.OperationalStaffOrInstructor);
        endpoints.MapPut("/api/attendance/{id:guid}", CorrectAsync).WithTags("Attendance").RequireAuthorization(TcmdPolicies.OperationalStaffOrInstructor);
        return endpoints;
    }

    private static async Task<IResult> RecordAsync(Guid sessionId, RecordAttendanceHttpRequest request,
        ClaimsPrincipal principal, RequestAccessResolver resolver, RecordAttendance useCase,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (sessionId == Guid.Empty) errors["sessionId"] = ["A valid sessionId is required."];
        if (request.EnrollmentId == Guid.Empty) errors["enrollmentId"] = ["A valid enrollmentId is required."];
        if (request.Status is null || !Enum.IsDefined(request.Status.Value)) errors["status"] = ["A valid attendance status is required."];
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        var access = await resolver.ResolveAsync(principal, cancellationToken);
        if (access is null) return Results.Forbid();
        var result = await useCase.ExecuteAsync(sessionId, new(request.EnrollmentId, request.Status!.Value),
            access.StaffUserId, access.InstructorId, cancellationToken);
        return result.Status == AttendanceMutationStatus.Success
            ? Results.Created($"/api/attendance/{result.Attendance!.Id}", result.Attendance) : MapError(result.Status);
    }

    private static async Task<IResult> CorrectAsync(Guid id, CorrectAttendanceHttpRequest request,
        ClaimsPrincipal principal, RequestAccessResolver resolver, CorrectAttendance useCase,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (id == Guid.Empty) errors["id"] = ["A valid attendance id is required."];
        if (request.Status is null || !Enum.IsDefined(request.Status.Value)) errors["status"] = ["A valid attendance status is required."];
        if (request.RowVersion is null || request.RowVersion.Length != 8) errors["rowVersion"] = ["A valid current rowVersion is required."];
        if (request.CorrectionNote?.Trim().Length > AttendanceRecord.CorrectionNoteMaxLength)
            errors["correctionNote"] = [$"Correction note must be {AttendanceRecord.CorrectionNoteMaxLength} characters or fewer."];
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        var access = await resolver.ResolveAsync(principal, cancellationToken);
        if (access is null) return Results.Forbid();
        var result = await useCase.ExecuteAsync(id, new(request.Status!.Value, request.CorrectionNote, request.RowVersion!),
            access.StaffUserId, access.InstructorId, cancellationToken);
        return result.Status == AttendanceMutationStatus.Success ? Results.Ok(result.Attendance) : MapError(result.Status);
    }

    private static async Task<IResult> ListSessionAsync(Guid sessionId, ListTrainingSessionAttendance useCase,
        ClaimsPrincipal principal, RequestAccessResolver resolver, CancellationToken token)
    { var access = await resolver.ResolveAsync(principal, token); if (access is null) return Results.Forbid(); var result = await useCase.ExecuteAsync(sessionId, access.InstructorId, token); return result.Found ? Results.Ok(result.Items) : Problem(404, "Training session not found"); }
    private static async Task<IResult> ListStudentAsync(Guid studentId, ListStudentAttendance useCase,
        ClaimsPrincipal principal, RequestAccessResolver resolver, CancellationToken token)
    { var access = await resolver.ResolveAsync(principal, token); if (access is null) return Results.Forbid(); var result = await useCase.ExecuteAsync(studentId, access.InstructorId, token); return result.Found ? Results.Ok(result.Items) : Problem(404, "Student not found"); }
    private static async Task<IResult> ListGroupAsync(Guid groupId, ListTrainingGroupAttendance useCase,
        ClaimsPrincipal principal, RequestAccessResolver resolver, CancellationToken token)
    { var access = await resolver.ResolveAsync(principal, token); if (access is null) return Results.Forbid(); var result = await useCase.ExecuteAsync(groupId, access.InstructorId, token); return result.Found ? Results.Ok(result.Items) : Problem(404, "Training group not found"); }

    private static IResult MapError(AttendanceMutationStatus status) => status switch
    {
        AttendanceMutationStatus.EnrollmentNotFound => Problem(404, "Enrollment not found"),
        AttendanceMutationStatus.SessionNotFound => Problem(404, "Training session not found"),
        AttendanceMutationStatus.AttendanceNotFound => Problem(404, "Attendance record not found"),
        AttendanceMutationStatus.DifferentTrainingGroup => Problem(409, "Enrollment and training session must belong to the same training group."),
        AttendanceMutationStatus.DuplicateAttendance => Problem(409, "Attendance has already been recorded for this enrollment and session."),
        AttendanceMutationStatus.EnrollmentInactive => Problem(409, "New attendance requires an Active enrollment."),
        AttendanceMutationStatus.EnrollmentAfterSession => Problem(409, "Enrollment date cannot be after the training session date."),
        AttendanceMutationStatus.SessionNotStarted => Problem(409, "Attendance cannot be recorded before the training session starts."),
        AttendanceMutationStatus.SessionCancelled => Problem(409, "Attendance cannot be recorded for a cancelled training session."),
        AttendanceMutationStatus.ConcurrencyConflict => Problem(409, "Attendance was changed by another user. Reload and try again."),
        AttendanceMutationStatus.AssignmentChanged => Problem(404, "Attendance resource not found"),
        _ => Problem(500, "An unexpected error occurred")
    };
    private static IResult Problem(int code, string title) => Results.Problem(statusCode: code, title: title);
}

public sealed record RecordAttendanceHttpRequest(Guid EnrollmentId, AttendanceStatus? Status);
public sealed record CorrectAttendanceHttpRequest(AttendanceStatus? Status, string? CorrectionNote, byte[]? RowVersion);
