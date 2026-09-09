using TCMD.Api.Authentication;
using TCMD.Application.Enrollments;

namespace TCMD.Api.Enrollments;

public static class EnrollmentEndpoints
{
    public static IEndpointRouteBuilder MapEnrollmentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/training-groups/{groupId:guid}/enrollments", CreateAsync)
            .WithTags("Enrollments").RequireAuthorization(TcmdPolicies.OperationalStaff);
        endpoints.MapGet("/api/training-groups/{groupId:guid}/enrollments", ListByGroupAsync)
            .WithTags("Enrollments").RequireAuthorization(TcmdPolicies.OperationalStaff);
        endpoints.MapGet("/api/students/{studentId:guid}/enrollments", ListByStudentAsync)
            .WithTags("Enrollments").RequireAuthorization(TcmdPolicies.OperationalStaff);
        var commands = endpoints.MapGroup("/api/enrollments").WithTags("Enrollments");
        commands.MapPost("/{id:guid}/complete", CompleteAsync).RequireAuthorization(TcmdPolicies.OperationalStaff);
        commands.MapPost("/{id:guid}/withdraw", WithdrawAsync).RequireAuthorization(TcmdPolicies.OperationalStaff);
        commands.MapPost("/{id:guid}/reactivate", ReactivateAsync).RequireAuthorization(TcmdPolicies.OperationalStaff);
        return endpoints;
    }

    private static async Task<IResult> CreateAsync(Guid groupId, CreateEnrollmentHttpRequest request,
        CreateEnrollment useCase, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (groupId == Guid.Empty) errors["groupId"] = ["A valid groupId is required."];
        if (request.StudentId == Guid.Empty) errors["studentId"] = ["A valid studentId is required."];
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        var result = await useCase.ExecuteAsync(groupId, request.StudentId, cancellationToken);
        return result.Status == EnrollmentMutationStatus.Success
            ? Results.Created($"/api/enrollments/{result.Enrollment!.Id}", result.Enrollment)
            : Map(result);
    }

    private static async Task<IResult> ListByGroupAsync(Guid groupId, ListTrainingGroupEnrollments useCase,
        CancellationToken cancellationToken)
    {
        if (groupId == Guid.Empty)
            return Results.ValidationProblem(new Dictionary<string, string[]>
                { ["groupId"] = ["A valid groupId is required."] });
        var result = await useCase.ExecuteAsync(groupId, cancellationToken);
        return result.Status == EnrollmentMutationStatus.Success
            ? Results.Ok(result.Enrollments) : MapError(result.Status);
    }

    private static async Task<IResult> ListByStudentAsync(Guid studentId, ListStudentEnrollments useCase,
        CancellationToken cancellationToken)
    {
        if (studentId == Guid.Empty)
            return Results.ValidationProblem(new Dictionary<string, string[]>
                { ["studentId"] = ["A valid studentId is required."] });
        var result = await useCase.ExecuteAsync(studentId, cancellationToken);
        return result.Status == EnrollmentMutationStatus.Success
            ? Results.Ok(result.Enrollments) : MapError(result.Status);
    }

    private static Task<IResult> CompleteAsync(Guid id, EnrollmentRowVersionHttpRequest request,
        CompleteEnrollment useCase, CancellationToken cancellationToken) =>
        ChangeStatusAsync(id, request, useCase.ExecuteAsync, cancellationToken);

    private static Task<IResult> WithdrawAsync(Guid id, EnrollmentRowVersionHttpRequest request,
        WithdrawEnrollment useCase, CancellationToken cancellationToken) =>
        ChangeStatusAsync(id, request, useCase.ExecuteAsync, cancellationToken);

    private static Task<IResult> ReactivateAsync(Guid id, EnrollmentRowVersionHttpRequest request,
        ReactivateEnrollment useCase, CancellationToken cancellationToken) =>
        ChangeStatusAsync(id, request, useCase.ExecuteAsync, cancellationToken);

    private static async Task<IResult> ChangeStatusAsync(Guid id, EnrollmentRowVersionHttpRequest request,
        Func<Guid, byte[], CancellationToken, Task<EnrollmentMutationResult>> execute,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return Results.ValidationProblem(new Dictionary<string, string[]>
                { ["id"] = ["A valid enrollment id is required."] });
        if (request.RowVersion is null || request.RowVersion.Length != 8)
            return Results.ValidationProblem(new Dictionary<string, string[]>
                { ["rowVersion"] = ["A valid current rowVersion is required."] });
        return Map(await execute(id, request.RowVersion, cancellationToken));
    }

    private static IResult Map(EnrollmentMutationResult result) => result.Status == EnrollmentMutationStatus.Success
        ? Results.Ok(result.Enrollment) : MapError(result.Status);

    private static IResult MapError(EnrollmentMutationStatus status) => status switch
    {
        EnrollmentMutationStatus.EnrollmentNotFound => Problem(404, "Enrollment not found"),
        EnrollmentMutationStatus.StudentNotFound => Problem(404, "Student not found"),
        EnrollmentMutationStatus.TrainingGroupNotFound => Problem(404, "Training group not found"),
        EnrollmentMutationStatus.StudentInactive => Problem(409, "An inactive student cannot be enrolled or reactivated."),
        EnrollmentMutationStatus.TrainingGroupIneligible => Problem(409, "Only Planned or Active training groups accept enrollment or reactivation."),
        EnrollmentMutationStatus.DuplicateEnrollment => Problem(409, "The student already has an enrollment in this training group."),
        EnrollmentMutationStatus.InvalidStatusTransition => Problem(409, "The requested enrollment status transition is not allowed."),
        EnrollmentMutationStatus.ConcurrencyConflict => Problem(409, "Enrollment was changed by another user. Reload and try again."),
        _ => Problem(500, "An unexpected error occurred")
    };

    private static IResult Problem(int statusCode, string title) => Results.Problem(statusCode: statusCode, title: title);
}

public sealed record CreateEnrollmentHttpRequest(Guid StudentId);
public sealed record EnrollmentRowVersionHttpRequest(byte[]? RowVersion);
