using Microsoft.AspNetCore.Identity;
using TCMD.Infrastructure.Identity;

namespace TCMD.Api.Authentication;

public static class AuthenticationEndpoints
{
    public static IEndpointRouteBuilder MapAuthenticationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/auth/login", LoginAsync).AllowAnonymous();
        endpoints.MapPost("/api/auth/logout", LogoutAsync);
        return endpoints;
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
