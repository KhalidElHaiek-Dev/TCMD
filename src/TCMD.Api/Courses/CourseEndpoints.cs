using TCMD.Api.Authentication;
using TCMD.Application.Courses;

namespace TCMD.Api.Courses;

public static class CourseEndpoints
{
    public static IEndpointRouteBuilder MapCourseEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/courses").WithTags("Courses");
        group.MapPost("/", CreateAsync).RequireAuthorization(TcmdPolicies.OperationalStaff);
        group.MapGet("/", SearchAsync).RequireAuthorization(TcmdPolicies.OperationalStaff);
        group.MapGet("/{id:guid}", GetByIdAsync).RequireAuthorization(TcmdPolicies.OperationalStaff);
        group.MapPut("/{id:guid}", UpdateAsync).RequireAuthorization(TcmdPolicies.OperationalStaff);
        group.MapPost("/{id:guid}/deactivate", DeactivateAsync).RequireAuthorization(TcmdPolicies.OperationalStaff);
        return endpoints;
    }

    private static async Task<IResult> CreateAsync(CreateCourseHttpRequest request, CreateCourse useCase, CancellationToken cancellationToken)
    {
        var errors = ValidateDetails(request.Code, request.Name, request.Description);
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        var result = await useCase.ExecuteAsync(new(request.Code!, request.Name!, NormalizeOptional(request.Description)), cancellationToken);
        return result.Status == CourseMutationStatus.Success
            ? Results.Created($"/api/courses/{result.Course!.Id}", result.Course)
            : Map(result);
    }

    private static async Task<IResult> SearchAsync(string? search, bool? isActive, SearchCourses useCase, CancellationToken cancellationToken) =>
        Results.Ok(await useCase.ExecuteAsync(search, isActive, cancellationToken));

    private static async Task<IResult> GetByIdAsync(Guid id, GetCourseById useCase, CancellationToken cancellationToken)
    {
        var course = await useCase.ExecuteAsync(id, cancellationToken);
        return course is null ? NotFound() : Results.Ok(course);
    }

    private static async Task<IResult> UpdateAsync(Guid id, UpdateCourseHttpRequest request, UpdateCourse useCase, CancellationToken cancellationToken)
    {
        var errors = ValidateDetails(request.Code, request.Name, request.Description);
        ValidateRowVersion(request.RowVersion, errors);
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        return Map(await useCase.ExecuteAsync(id,
            new(request.Code!, request.Name!, NormalizeOptional(request.Description), request.RowVersion!), cancellationToken));
    }

    private static async Task<IResult> DeactivateAsync(Guid id, CourseRowVersionHttpRequest request, DeactivateCourse useCase, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        ValidateRowVersion(request.RowVersion, errors);
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        return Map(await useCase.ExecuteAsync(id, request.RowVersion!, cancellationToken));
    }

    private static IResult Map(CourseMutationResult result) => result.Status switch
    {
        CourseMutationStatus.Success => Results.Ok(result.Course),
        CourseMutationStatus.NotFound => NotFound(),
        CourseMutationStatus.ConcurrencyConflict => Results.Problem(statusCode: 409,
            title: "Course was changed by another user. Reload and try again."),
        CourseMutationStatus.DuplicateCode => Results.Problem(statusCode: 409, title: "Course code is already in use."),
        _ => Results.Problem(statusCode: 500)
    };

    private static Dictionary<string, string[]> ValidateDetails(string? codeValue, string? nameValue, string? descriptionValue)
    {
        var errors = new Dictionary<string, string[]>();
        var code = codeValue?.Trim();
        if (string.IsNullOrWhiteSpace(code)) errors["code"] = ["Code is required."];
        else if (code.Length > 50) errors["code"] = ["Code must be 50 characters or fewer."];
        else if (code.Any(character => !char.IsLetterOrDigit(character) && character is not (' ' or '-' or '_' or '.' or '/')))
            errors["code"] = ["Code may contain only letters, digits, spaces, hyphens, underscores, periods, and slashes."];

        var name = nameValue?.Trim();
        if (string.IsNullOrWhiteSpace(name)) errors["name"] = ["Name is required."];
        else if (name.Length > 200) errors["name"] = ["Name must be 200 characters or fewer."];

        var description = NormalizeOptional(descriptionValue);
        if (description?.Length > 2000) errors["description"] = ["Description must be 2000 characters or fewer."];
        return errors;
    }

    private static void ValidateRowVersion(byte[]? rowVersion, Dictionary<string, string[]> errors)
    { if (rowVersion is null || rowVersion.Length != 8) errors["rowVersion"] = ["A valid current rowVersion is required."]; }

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static IResult NotFound() => Results.Problem(statusCode: 404, title: "Course not found");
}

public sealed record CreateCourseHttpRequest(string? Code, string? Name, string? Description);
public sealed record UpdateCourseHttpRequest(string? Code, string? Name, string? Description, byte[]? RowVersion);
public sealed record CourseRowVersionHttpRequest(byte[]? RowVersion);
