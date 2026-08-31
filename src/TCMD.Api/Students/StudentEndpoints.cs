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
        group.MapGet("/{id:guid}", GetByIdAsync).RequireAuthorization(TcmdPolicies.OperationalStaff);
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
        return student is null
            ? Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Student not found")
            : Results.Ok(student);
    }

    private static Dictionary<string, string[]> Validate(RegisterStudentHttpRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            errors["fullName"] = ["Full name is required."];
        }
        else if (request.FullName.Trim().Length > 200)
        {
            errors["fullName"] = ["Full name must be 200 characters or fewer."];
        }

        if (string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            errors["phoneNumber"] = ["Phone number is required."];
        }
        else if (request.PhoneNumber.Trim().Length > 50)
        {
            errors["phoneNumber"] = ["Phone number must be 50 characters or fewer."];
        }

        var email = NormalizeEmail(request.Email);
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

    private static string? NormalizeEmail(string? email) =>
        string.IsNullOrWhiteSpace(email) ? null : email.Trim();
}

public sealed record RegisterStudentHttpRequest(string? FullName, string? PhoneNumber, string? Email);
