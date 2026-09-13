using TCMD.Api.Authentication;
using TCMD.Application.TrainingSessions;
using System.Security.Claims;

namespace TCMD.Api.TrainingSessions;

public static class TrainingSessionEndpoints
{
    public static IEndpointRouteBuilder MapTrainingSessionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/training-groups/{groupId:guid}/sessions", CreateAsync)
            .WithTags("Training Sessions").RequireAuthorization(TcmdPolicies.OperationalStaff);
        endpoints.MapGet("/api/training-groups/{groupId:guid}/sessions", ListAsync)
            .WithTags("Training Sessions").RequireAuthorization(TcmdPolicies.OperationalStaffOrInstructor);
        var sessions = endpoints.MapGroup("/api/training-sessions").WithTags("Training Sessions");
        sessions.MapGet("/{id:guid}", GetAsync).RequireAuthorization(TcmdPolicies.OperationalStaffOrInstructor);
        sessions.MapPut("/{id:guid}", UpdateAsync).RequireAuthorization(TcmdPolicies.OperationalStaff);
        sessions.MapPost("/{id:guid}/complete", CompleteAsync).RequireAuthorization(TcmdPolicies.OperationalStaff);
        sessions.MapPost("/{id:guid}/cancel", CancelAsync).RequireAuthorization(TcmdPolicies.OperationalStaff);
        return endpoints;
    }

    private static async Task<IResult> CreateAsync(Guid groupId, CreateTrainingSessionHttpRequest request,
        CreateTrainingSession useCase, CancellationToken cancellationToken)
    {
        var errors = ValidateDetails(groupId, request.SessionDate, request.StartTime, request.EndTime, request.Location);
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        var result = await useCase.ExecuteAsync(groupId,
            new(request.SessionDate, request.StartTime, request.EndTime, NormalizeLocation(request.Location)), cancellationToken);
        return result.Status == TrainingSessionMutationStatus.Success
            ? Results.Created($"/api/training-sessions/{result.Session!.Id}", result.Session) : MapError(result.Status);
    }

    private static async Task<IResult> ListAsync(Guid groupId, ListTrainingGroupSessions useCase,
        ClaimsPrincipal principal, RequestAccessResolver resolver, CancellationToken cancellationToken)
    {
        var access = await resolver.ResolveAsync(principal, cancellationToken);
        if (access is null) return Results.Forbid();
        var result = await useCase.ExecuteAsync(groupId, access.InstructorId, cancellationToken);
        return result.Status == TrainingSessionMutationStatus.Success
            ? Results.Ok(result.Sessions) : MapError(result.Status);
    }

    private static async Task<IResult> GetAsync(Guid id, GetTrainingSessionById useCase,
        ClaimsPrincipal principal, RequestAccessResolver resolver, CancellationToken cancellationToken)
    {
        var access = await resolver.ResolveAsync(principal, cancellationToken);
        if (access is null) return Results.Forbid();
        var session = await useCase.ExecuteAsync(id, access.InstructorId, cancellationToken);
        return session is null ? Problem(404, "Training session not found") : Results.Ok(session);
    }

    private static async Task<IResult> UpdateAsync(Guid id, UpdateTrainingSessionHttpRequest request,
        UpdateTrainingSession useCase, CancellationToken cancellationToken)
    {
        var errors = ValidateDetails(null, request.SessionDate, request.StartTime, request.EndTime, request.Location);
        ValidateRowVersion(request.RowVersion, errors);
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        return Map(await useCase.ExecuteAsync(id, new(request.SessionDate, request.StartTime, request.EndTime,
            NormalizeLocation(request.Location), request.RowVersion!), cancellationToken));
    }

    private static Task<IResult> CompleteAsync(Guid id, TrainingSessionRowVersionHttpRequest request,
        CompleteTrainingSession useCase, CancellationToken cancellationToken) =>
        ChangeStatusAsync(id, request, useCase.ExecuteAsync, cancellationToken);

    private static Task<IResult> CancelAsync(Guid id, TrainingSessionRowVersionHttpRequest request,
        CancelTrainingSession useCase, CancellationToken cancellationToken) =>
        ChangeStatusAsync(id, request, useCase.ExecuteAsync, cancellationToken);

    private static async Task<IResult> ChangeStatusAsync(Guid id, TrainingSessionRowVersionHttpRequest request,
        Func<Guid, byte[], CancellationToken, Task<TrainingSessionMutationResult>> execute,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        ValidateRowVersion(request.RowVersion, errors);
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        return Map(await execute(id, request.RowVersion!, cancellationToken));
    }

    private static IResult Map(TrainingSessionMutationResult result) =>
        result.Status == TrainingSessionMutationStatus.Success ? Results.Ok(result.Session) : MapError(result.Status);

    private static IResult MapError(TrainingSessionMutationStatus status) => status switch
    {
        TrainingSessionMutationStatus.SessionNotFound => Problem(404, "Training session not found"),
        TrainingSessionMutationStatus.TrainingGroupNotFound => Problem(404, "Training group not found"),
        TrainingSessionMutationStatus.TrainingGroupIneligible => Problem(409, "Only Planned or Active training groups allow session creation or detail updates."),
        TrainingSessionMutationStatus.SessionDateOutsideGroupRange => Problem(409, "Session date must be within the training group's planned date range."),
        TrainingSessionMutationStatus.TerminalSession => Problem(409, "Completed or Cancelled sessions cannot be edited."),
        TrainingSessionMutationStatus.InvalidStatusTransition => Problem(409, "The requested session status transition is not allowed."),
        TrainingSessionMutationStatus.ConcurrencyConflict => Problem(409, "Training session was changed by another user. Reload and try again."),
        _ => Problem(500, "An unexpected error occurred")
    };

    private static Dictionary<string, string[]> ValidateDetails(Guid? groupId, DateOnly date, TimeOnly start,
        TimeOnly end, string? location)
    {
        var errors = new Dictionary<string, string[]>();
        if (groupId == Guid.Empty) errors["groupId"] = ["A valid groupId is required."];
        if (date == default) errors["sessionDate"] = ["Session date is required."];
        if (end <= start) errors["endTime"] = ["End time must be later than start time."];
        if (location?.Trim().Length > 500) errors["location"] = ["Location must be 500 characters or fewer."];
        return errors;
    }

    private static void ValidateRowVersion(byte[]? rowVersion, Dictionary<string, string[]> errors)
    { if (rowVersion is null || rowVersion.Length != 8) errors["rowVersion"] = ["A valid current rowVersion is required."]; }

    private static string? NormalizeLocation(string? location) => string.IsNullOrWhiteSpace(location) ? null : location.Trim();
    private static IResult Problem(int statusCode, string title) => Results.Problem(statusCode: statusCode, title: title);
}

public sealed record CreateTrainingSessionHttpRequest(DateOnly SessionDate, TimeOnly StartTime,
    TimeOnly EndTime, string? Location);
public sealed record UpdateTrainingSessionHttpRequest(DateOnly SessionDate, TimeOnly StartTime,
    TimeOnly EndTime, string? Location, byte[]? RowVersion);
public sealed record TrainingSessionRowVersionHttpRequest(byte[]? RowVersion);
