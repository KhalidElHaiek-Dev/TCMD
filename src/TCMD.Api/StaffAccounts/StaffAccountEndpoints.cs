using System.Security.Claims;
using TCMD.Api.Authentication;
using TCMD.Application.StaffAccounts;

namespace TCMD.Api.StaffAccounts;

public static class StaffAccountEndpoints
{
    public static IEndpointRouteBuilder MapStaffAccountEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/staff-accounts").WithTags("Staff Accounts")
            .RequireAuthorization(TcmdPolicies.Administrators);
        group.MapGet("/", ListAsync);
        group.MapGet("/{id:guid}", GetAsync);
        group.MapPost("/", CreateAsync);
        group.MapPatch("/{id:guid}/active", SetActiveAsync);
        group.MapPatch("/{id:guid}/role", ChangeRoleAsync);
        group.MapPost("/{id:guid}/password", ReplacePasswordAsync);
        return endpoints;
    }

    private static async Task<IResult> ListAsync(StaffAccountAdministration accounts, CancellationToken cancellationToken) =>
        Results.Ok(await accounts.ListAsync(cancellationToken));

    private static async Task<IResult> GetAsync(Guid id, StaffAccountAdministration accounts, CancellationToken cancellationToken)
    {
        var account = await accounts.GetAsync(id, cancellationToken);
        return account is null ? NotFound() : Results.Ok(account);
    }

    private static async Task<IResult> CreateAsync(CreateStaffAccountHttpRequest request,
        StaffAccountAdministration accounts, CancellationToken cancellationToken)
    {
        var errors = Validate(request.UserName, request.DisplayName, request.Password, request.Role);
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        var result = await accounts.CreateAsync(new(request.UserName!.Trim(), request.DisplayName!.Trim(), request.Password!, request.Role!), cancellationToken);
        return result.Succeeded ? Results.Created($"/api/staff-accounts/{result.Account!.Id}", result.Account) : MapError(result.Error);
    }

    private static async Task<IResult> SetActiveAsync(Guid id, SetAccountActiveRequest request, ClaimsPrincipal principal,
        StaffAccountAdministration accounts, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId))
            return Results.Unauthorized();
        return MapResult(await accounts.SetActiveAsync(id, request.IsActive, currentUserId, cancellationToken));
    }

    private static async Task<IResult> ChangeRoleAsync(Guid id, ChangeAccountRoleRequest request,
        StaffAccountAdministration accounts, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Role)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["role"] = ["Role is required."] });
        return MapResult(await accounts.ChangeRoleAsync(id, request.Role, cancellationToken));
    }

    private static async Task<IResult> ReplacePasswordAsync(Guid id, ReplaceAccountPasswordRequest request,
        StaffAccountAdministration accounts, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["newPassword"] = ["New password is required."] });
        return MapResult(await accounts.ReplacePasswordAsync(id, request.NewPassword, cancellationToken), "newPassword");
    }

    private static IResult MapResult(StaffAccountResult result, string passwordField = "password") =>
        result.Succeeded ? Results.Ok(result.Account) : MapError(result.Error, passwordField);

    private static IResult MapError(StaffAccountError error, string passwordField = "password") => error switch
    {
        StaffAccountError.NotFound => NotFound(),
        StaffAccountError.DuplicateUserName => Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Username is already in use."),
        StaffAccountError.InvalidUserName => Results.ValidationProblem(new Dictionary<string, string[]> { ["userName"] = ["Username contains unsupported characters."] }),
        StaffAccountError.InvalidPassword => Results.ValidationProblem(new Dictionary<string, string[]> { [passwordField] = ["Password does not meet the required policy."] }),
        StaffAccountError.InvalidRole => Results.ValidationProblem(new Dictionary<string, string[]> { ["role"] = ["Role must be Administrator, Staff, or Instructor."] }),
        StaffAccountError.CannotDeactivateSelf => Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Administrators cannot deactivate their own account."),
        StaffAccountError.LastActiveAdministrator => Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "The last active administrator cannot be deactivated or demoted."),
        _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError)
    };
    private static IResult NotFound() => Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Staff account not found");

    private static Dictionary<string, string[]> Validate(string? userName, string? displayName, string? password, string? role)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(userName)) errors["userName"] = ["Username is required."];
        else if (userName.Trim().Length > 256) errors["userName"] = ["Username must be 256 characters or fewer."];
        if (string.IsNullOrWhiteSpace(displayName)) errors["displayName"] = ["Display name is required."];
        if (string.IsNullOrWhiteSpace(password)) errors["password"] = ["Password is required."];
        if (string.IsNullOrWhiteSpace(role)) errors["role"] = ["Role is required."];
        return errors;
    }
}

public sealed record CreateStaffAccountHttpRequest(string? UserName, string? DisplayName, string? Password, string? Role);
public sealed record SetAccountActiveRequest(bool IsActive);
public sealed record ChangeAccountRoleRequest(string? Role);
public sealed record ReplaceAccountPasswordRequest(string? NewPassword);
