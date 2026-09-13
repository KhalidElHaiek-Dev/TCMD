using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using TCMD.Application.StaffAccounts;
using TCMD.Infrastructure.Identity;

namespace TCMD.Api.Authentication;

public static class AuthenticationEndpoints
{
    public static IEndpointRouteBuilder MapAuthenticationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/auth/login", LoginAsync).AllowAnonymous();
        endpoints.MapPost("/api/auth/logout", LogoutAsync);
        endpoints.MapGet("/api/auth/antiforgery", AntiforgeryAsync).AllowAnonymous();
        endpoints.MapGet("/api/auth/session", SessionAsync);
        return endpoints;
    }

    private static IResult AntiforgeryAsync(HttpContext context, IAntiforgery antiforgery)
    {
        var tokens = antiforgery.GetAndStoreTokens(context);
        return Results.Ok(new { requestToken = tokens.RequestToken });
    }

    private static async Task<IResult> SessionAsync(ClaimsPrincipal principal, UserManager<StaffUser> userManager,
        IInstructorAccessStore instructorAccess, CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null || !user.IsActive) return Results.Unauthorized();
        var roles = await userManager.GetRolesAsync(user);
        var role = roles.SingleOrDefault();
        if (role is null) return Results.Forbid();

        if (role != StaffRoles.Instructor)
            return Results.Ok(new SessionResponse(user.Id, user.UserName!, user.DisplayName, role, null, "NotApplicable"));

        var identity = await instructorAccess.ResolveAsync(user.Id, cancellationToken);
        return Results.Ok(new SessionResponse(user.Id, user.UserName!, user.DisplayName, role,
            identity.InstructorId, identity.Status.ToString()));
    }

    private static async Task<IResult> LoginAsync(LoginRequest request, UserManager<StaffUser> userManager,
        SignInManager<StaffUser> signInManager)
    {
        if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Password))
        {
            return InvalidSignIn();
        }

        var user = await userManager.FindByNameAsync(request.UserName.Trim());
        if (user is null || !user.IsActive)
        {
            return InvalidSignIn();
        }

        var result = await signInManager.PasswordSignInAsync(user, request.Password, false, true);
        return result.Succeeded ? Results.NoContent() : InvalidSignIn();
    }

    private static async Task<IResult> LogoutAsync(SignInManager<StaffUser> signInManager)
    {
        await signInManager.SignOutAsync();
        return Results.NoContent();
    }

    private static IResult InvalidSignIn() => Results.Problem(statusCode: StatusCodes.Status401Unauthorized,
        title: "Invalid sign-in details");
}

public sealed record SessionResponse(Guid Id, string UserName, string DisplayName, string Role,
    Guid? InstructorId, string InstructorLinkStatus);
