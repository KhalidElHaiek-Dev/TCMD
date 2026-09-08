using System.ComponentModel.DataAnnotations;
using System.Net.Mail;
using TCMD.Api.Authentication;
using TCMD.Application.Instructors;

namespace TCMD.Api.Instructors;

public static class InstructorEndpoints
{
    public static IEndpointRouteBuilder MapInstructorEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/instructors").WithTags("Instructors");
        group.MapPost("/", CreateAsync).RequireAuthorization(TcmdPolicies.OperationalStaff);
        group.MapGet("/", SearchAsync).RequireAuthorization(TcmdPolicies.OperationalStaff);
        group.MapGet("/{id:guid}", GetByIdAsync).RequireAuthorization(TcmdPolicies.OperationalStaff);
        group.MapPut("/{id:guid}", UpdateAsync).RequireAuthorization(TcmdPolicies.OperationalStaff);
        group.MapPost("/{id:guid}/deactivate", DeactivateAsync).RequireAuthorization(TcmdPolicies.OperationalStaff);
        return endpoints;
    }

    private static async Task<IResult> CreateAsync(CreateInstructorHttpRequest request, CreateInstructor useCase, CancellationToken cancellationToken)
    {
        var errors = ValidateDetails(request.FullName, request.PhoneNumber, request.Email);
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        var instructor = await useCase.ExecuteAsync(new(request.FullName!, NormalizeOptional(request.PhoneNumber), NormalizeOptional(request.Email)), cancellationToken);
        return Results.Created($"/api/instructors/{instructor.Id}", instructor);
    }

    private static async Task<IResult> SearchAsync(string? search, bool? isActive, SearchInstructors useCase, CancellationToken cancellationToken) =>
        Results.Ok(await useCase.ExecuteAsync(search, isActive, cancellationToken));

    private static async Task<IResult> GetByIdAsync(Guid id, GetInstructorById useCase, CancellationToken cancellationToken)
    {
        var instructor = await useCase.ExecuteAsync(id, cancellationToken);
        return instructor is null ? NotFound() : Results.Ok(instructor);
    }

    private static async Task<IResult> UpdateAsync(Guid id, UpdateInstructorHttpRequest request, UpdateInstructor useCase, CancellationToken cancellationToken)
    {
        var errors = ValidateDetails(request.FullName, request.PhoneNumber, request.Email);
        ValidateRowVersion(request.RowVersion, errors);
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        return Map(await useCase.ExecuteAsync(id, new(request.FullName!, NormalizeOptional(request.PhoneNumber), NormalizeOptional(request.Email), request.RowVersion!), cancellationToken));
    }

    private static async Task<IResult> DeactivateAsync(Guid id, InstructorRowVersionHttpRequest request, DeactivateInstructor useCase, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        ValidateRowVersion(request.RowVersion, errors);
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        return Map(await useCase.ExecuteAsync(id, request.RowVersion!, cancellationToken));
    }

    private static IResult Map(InstructorMutationResult result) => result.Status switch
    {
        InstructorMutationStatus.Success => Results.Ok(result.Instructor),
        InstructorMutationStatus.NotFound => NotFound(),
        InstructorMutationStatus.Conflict => Results.Problem(statusCode: 409, title: "Instructor was changed by another user. Reload and try again."),
        _ => Results.Problem(statusCode: 500)
    };

    private static Dictionary<string, string[]> ValidateDetails(string? fullName, string? phone, string? emailValue)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(fullName)) errors["fullName"] = ["Full name is required."];
        else if (fullName.Trim().Length > 200) errors["fullName"] = ["Full name must be 200 characters or fewer."];
        var normalizedPhone = NormalizeOptional(phone);
        if (normalizedPhone?.Length > 50) errors["phoneNumber"] = ["Phone number must be 50 characters or fewer."];
        var email = NormalizeOptional(emailValue);
        if (email is not null && (!new EmailAddressAttribute().IsValid(email) ||
            !MailAddress.TryCreate(email, out var parsed) || parsed.Address != email))
            errors["email"] = ["Email must be a valid email address."];
        else if (email?.Length > 320) errors["email"] = ["Email must be 320 characters or fewer."];
        return errors;
    }

    private static void ValidateRowVersion(byte[]? rowVersion, Dictionary<string, string[]> errors)
    { if (rowVersion is null || rowVersion.Length != 8) errors["rowVersion"] = ["A valid current rowVersion is required."]; }
    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static IResult NotFound() => Results.Problem(statusCode: 404, title: "Instructor not found");
}

public sealed record CreateInstructorHttpRequest(string? FullName, string? PhoneNumber, string? Email);
public sealed record UpdateInstructorHttpRequest(string? FullName, string? PhoneNumber, string? Email, byte[]? RowVersion);
public sealed record InstructorRowVersionHttpRequest(byte[]? RowVersion);
