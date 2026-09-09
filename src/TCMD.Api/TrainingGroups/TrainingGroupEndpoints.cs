using TCMD.Api.Authentication;
using TCMD.Application.TrainingGroups;
using TCMD.Domain.TrainingGroups;

namespace TCMD.Api.TrainingGroups;

public static class TrainingGroupEndpoints
{
    public static IEndpointRouteBuilder MapTrainingGroupEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/training-groups").WithTags("Training Groups");
        group.MapPost("/", CreateAsync).RequireAuthorization(TcmdPolicies.OperationalStaff);
        group.MapGet("/", SearchAsync).RequireAuthorization(TcmdPolicies.OperationalStaff);
        group.MapGet("/{id:guid}", GetByIdAsync).RequireAuthorization(TcmdPolicies.OperationalStaff);
        group.MapPut("/{id:guid}", UpdateAsync).RequireAuthorization(TcmdPolicies.OperationalStaff);
        group.MapPost("/{id:guid}/activate", ActivateAsync).RequireAuthorization(TcmdPolicies.OperationalStaff);
        group.MapPost("/{id:guid}/complete", CompleteAsync).RequireAuthorization(TcmdPolicies.OperationalStaff);
        group.MapPost("/{id:guid}/cancel", CancelAsync).RequireAuthorization(TcmdPolicies.OperationalStaff);
        return endpoints;
    }

    private static async Task<IResult> CreateAsync(CreateTrainingGroupHttpRequest request, CreateTrainingGroup useCase,
        CancellationToken cancellationToken)
    {
        var errors = ValidateDetails(request.Name, request.CourseId, request.PrimaryInstructorId,
            request.PlannedStartDate, request.PlannedEndDate);
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        var result = await useCase.ExecuteAsync(new(request.Name!, request.CourseId, request.PrimaryInstructorId,
            request.PlannedStartDate, request.PlannedEndDate), cancellationToken);
        return result.Status == TrainingGroupMutationStatus.Success
            ? Results.Created($"/api/training-groups/{result.Group!.Id}", result.Group)
            : Map(result);
    }

    private static async Task<IResult> SearchAsync(string? search, string? status, Guid? courseId,
        Guid? primaryInstructorId, bool? hasPrimaryInstructor, SearchTrainingGroups useCase,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        TrainingGroupStatus? parsedStatus = null;
        if (status is not null)
        {
            if (!Enum.TryParse<TrainingGroupStatus>(status, true, out var candidate) || !Enum.IsDefined(candidate))
                errors["status"] = ["Status must be Planned, Active, Completed, or Cancelled."];
            else parsedStatus = candidate;
        }
        if (courseId == Guid.Empty) errors["courseId"] = ["courseId cannot be empty."];
        if (primaryInstructorId == Guid.Empty) errors["primaryInstructorId"] = ["primaryInstructorId cannot be empty."];
        if (primaryInstructorId is not null && hasPrimaryInstructor == false)
            errors["hasPrimaryInstructor"] = ["hasPrimaryInstructor cannot be false when primaryInstructorId is supplied."];
        return errors.Count > 0 ? Results.ValidationProblem(errors) : Results.Ok(await useCase.ExecuteAsync(search,
            parsedStatus, courseId, primaryInstructorId, hasPrimaryInstructor, cancellationToken));
    }

    private static async Task<IResult> GetByIdAsync(Guid id, GetTrainingGroupById useCase,
        CancellationToken cancellationToken)
    {
        var group = await useCase.ExecuteAsync(id, cancellationToken);
        return group is null ? NotFound() : Results.Ok(group);
    }

    private static async Task<IResult> UpdateAsync(Guid id, UpdateTrainingGroupHttpRequest request,
        UpdateTrainingGroup useCase, CancellationToken cancellationToken)
    {
        var errors = ValidateDetails(request.Name, request.CourseId, request.PrimaryInstructorId,
            request.PlannedStartDate, request.PlannedEndDate);
        ValidateRowVersion(request.RowVersion, errors);
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        return Map(await useCase.ExecuteAsync(id, new(request.Name!, request.CourseId, request.PrimaryInstructorId,
            request.PlannedStartDate, request.PlannedEndDate, request.RowVersion!), cancellationToken));
    }

    private static Task<IResult> ActivateAsync(Guid id, TrainingGroupRowVersionHttpRequest request,
        ActivateTrainingGroup useCase, CancellationToken cancellationToken) =>
        ChangeStatusAsync(id, request, useCase.ExecuteAsync, cancellationToken);

    private static Task<IResult> CompleteAsync(Guid id, TrainingGroupRowVersionHttpRequest request,
        CompleteTrainingGroup useCase, CancellationToken cancellationToken) =>
        ChangeStatusAsync(id, request, useCase.ExecuteAsync, cancellationToken);

    private static Task<IResult> CancelAsync(Guid id, TrainingGroupRowVersionHttpRequest request,
        CancelTrainingGroup useCase, CancellationToken cancellationToken) =>
        ChangeStatusAsync(id, request, useCase.ExecuteAsync, cancellationToken);

    private static async Task<IResult> ChangeStatusAsync(Guid id, TrainingGroupRowVersionHttpRequest request,
        Func<Guid, byte[], CancellationToken, Task<TrainingGroupMutationResult>> execute,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        ValidateRowVersion(request.RowVersion, errors);
        return errors.Count > 0 ? Results.ValidationProblem(errors) : Map(await execute(id, request.RowVersion!, cancellationToken));
    }

    private static IResult Map(TrainingGroupMutationResult result) => result.Status switch
    {
        TrainingGroupMutationStatus.Success => Results.Ok(result.Group),
        TrainingGroupMutationStatus.NotFound => NotFound(),
        TrainingGroupMutationStatus.CourseNotFound => Results.Problem(statusCode: 404, title: "Course not found"),
        TrainingGroupMutationStatus.InstructorNotFound => Results.Problem(statusCode: 404, title: "Instructor not found"),
        TrainingGroupMutationStatus.CourseInactive => Results.Problem(statusCode: 409, title: "An inactive course cannot be selected for this operation."),
        TrainingGroupMutationStatus.InstructorInactive => Results.Problem(statusCode: 409, title: "An inactive instructor cannot be selected for this operation."),
        TrainingGroupMutationStatus.InstructorRequired => Results.Problem(statusCode: 409, title: "A primary instructor is required before activation."),
        TrainingGroupMutationStatus.ProhibitedChange => Results.Problem(statusCode: 409, title: "The requested changes are not allowed for the group's current status."),
        TrainingGroupMutationStatus.InvalidStatusTransition => Results.Problem(statusCode: 409, title: "The requested group status transition is not allowed."),
        TrainingGroupMutationStatus.ConcurrencyConflict => Results.Problem(statusCode: 409, title: "Training group was changed by another user. Reload and try again."),
        TrainingGroupMutationStatus.DuplicateGroup => Results.Problem(statusCode: 409, title: "A group with the same course, name, and planned start date already exists."),
        _ => Results.Problem(statusCode: 500)
    };

    private static Dictionary<string, string[]> ValidateDetails(string? nameValue, Guid courseId,
        Guid? instructorId, DateOnly start, DateOnly end)
    {
        var errors = new Dictionary<string, string[]>();
        var name = nameValue?.Trim();
        if (string.IsNullOrWhiteSpace(name)) errors["name"] = ["Name is required."];
        else if (name.Length > 200) errors["name"] = ["Name must be 200 characters or fewer."];
        if (courseId == Guid.Empty) errors["courseId"] = ["A valid courseId is required."];
        if (instructorId == Guid.Empty) errors["primaryInstructorId"] = ["primaryInstructorId cannot be empty."];
        if (start == default) errors["plannedStartDate"] = ["Planned start date is required."];
        if (end == default) errors["plannedEndDate"] = ["Planned end date is required."];
        else if (start != default && end < start)
            errors["plannedEndDate"] = ["Planned end date cannot be before planned start date."];
        return errors;
    }

    private static void ValidateRowVersion(byte[]? rowVersion, Dictionary<string, string[]> errors)
    { if (rowVersion is null || rowVersion.Length != 8) errors["rowVersion"] = ["A valid current rowVersion is required."]; }

    private static IResult NotFound() => Results.Problem(statusCode: 404, title: "Training group not found");
}

public sealed record CreateTrainingGroupHttpRequest(string? Name, Guid CourseId, Guid? PrimaryInstructorId,
    DateOnly PlannedStartDate, DateOnly PlannedEndDate);
public sealed record UpdateTrainingGroupHttpRequest(string? Name, Guid CourseId, Guid? PrimaryInstructorId,
    DateOnly PlannedStartDate, DateOnly PlannedEndDate, byte[]? RowVersion);
public sealed record TrainingGroupRowVersionHttpRequest(byte[]? RowVersion);
