using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using TCMD.Api.Authentication;
using TCMD.Application.Students;

namespace TCMD.Api.Students;

public static class StudentEndpoints
{
    public static IEndpointRouteBuilder MapStudentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/students").WithTags("Students");
        group.MapPost("/", RegisterAsync).RequireAuthorization(TcmdPolicies.OperationalStaff);
        group.MapGet("/", SearchAsync).RequireAuthorization(TcmdPolicies.OperationalStaff);
        group.MapGet("/{id:guid}", GetByIdAsync).RequireAuthorization(TcmdPolicies.OperationalStaff);
        group.MapPut("/{id:guid}", UpdateAsync).RequireAuthorization(TcmdPolicies.OperationalStaff);
        group.MapPost("/{id:guid}/deactivate", DeactivateAsync).RequireAuthorization(TcmdPolicies.OperationalStaff);
        return endpoints;
    }

    private static async Task<IResult> RegisterAsync(
        RegisterStudentHttpRequest request,
        RegisterStudent useCase,
        CancellationToken cancellationToken)
    {
        var errors = Validate(request);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var student = await useCase.ExecuteAsync(
            new RegisterStudentRequest(request.FullName!, request.PhoneNumber!, NormalizeEmail(request.Email)),
            cancellationToken);

        return Results.Created($"/api/students/{student.Id}", student);
    }

    private static async Task<IResult> GetByIdAsync(
        Guid id,
        GetStudentById useCase,
        CancellationToken cancellationToken)
    {
        var student = await useCase.ExecuteAsync(id, cancellationToken);
        return student is null ? StudentNotFound() : Results.Ok(student);
    }

    private static async Task<IResult> SearchAsync(
        string? search,
        bool? isActive,
        SearchStudents useCase,
        CancellationToken cancellationToken) =>
        Results.Ok(await useCase.ExecuteAsync(search, isActive, cancellationToken));

    private static async Task<IResult> UpdateAsync(
        Guid id,
        UpdateStudentHttpRequest request,
        UpdateStudent useCase,
        CancellationToken cancellationToken)
    {
        var errors = ValidateDetails(request.FullName, request.PhoneNumber, request.Email);
        ValidateRowVersion(request.RowVersion, errors);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var result = await useCase.ExecuteAsync(
            id,
            new UpdateStudentRequest(
                request.FullName!,
                request.PhoneNumber!,
                NormalizeEmail(request.Email),
                request.RowVersion!),
            cancellationToken);

        return MapMutationResult(result);
    }

    private static async Task<IResult> DeactivateAsync(
        Guid id,
        StudentRowVersionHttpRequest request,
        DeactivateStudent useCase,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        ValidateRowVersion(request.RowVersion, errors);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        return MapMutationResult(await useCase.ExecuteAsync(id, request.RowVersion!, cancellationToken));
    }

    private static IResult MapMutationResult(StudentMutationResult result) => result.Status switch
    {
        StudentMutationStatus.Success => Results.Ok(result.Student),
        StudentMutationStatus.NotFound => StudentNotFound(),
        StudentMutationStatus.Conflict => Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Student was changed by another user. Reload and try again."),
        _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError)
    };

    private static Dictionary<string, string[]> Validate(RegisterStudentHttpRequest request)
        => ValidateDetails(request.FullName, request.PhoneNumber, request.Email);

    private static Dictionary<string, string[]> ValidateDetails(
        string? fullName,
        string? phoneNumber,
        string? emailValue)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(fullName))
        {
            errors["fullName"] = ["Full name is required."];
        }
        else if (fullName.Trim().Length > 200)
        {
            errors["fullName"] = ["Full name must be 200 characters or fewer."];
        }

        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            errors["phoneNumber"] = ["Phone number is required."];
        }
        else if (phoneNumber.Trim().Length > 50)
        {
            errors["phoneNumber"] = ["Phone number must be 50 characters or fewer."];
        }

        var email = NormalizeEmail(emailValue);
        if (email is not null && !new EmailAddressAttribute().IsValid(email))
        {
            errors["email"] = ["Email must be a valid email address."];
        }
        else if (email?.Length > 320)
        {
            errors["email"] = ["Email must be 320 characters or fewer."];
        }

        return errors;
    }

    private static void ValidateRowVersion(byte[]? rowVersion, Dictionary<string, string[]> errors)
    {
        if (rowVersion is null || rowVersion.Length != 8)
        {
            errors["rowVersion"] = ["A valid current rowVersion is required."];
        }
    }

    private static IResult StudentNotFound() =>
        Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Student not found");

    private static string? NormalizeEmail(string? email) =>
        string.IsNullOrWhiteSpace(email) ? null : email.Trim();
}

public sealed record RegisterStudentHttpRequest(string? FullName, string? PhoneNumber, string? Email);
public sealed record UpdateStudentHttpRequest(string? FullName, string? PhoneNumber, string? Email, byte[]? RowVersion);
public sealed record StudentRowVersionHttpRequest(byte[]? RowVersion);
