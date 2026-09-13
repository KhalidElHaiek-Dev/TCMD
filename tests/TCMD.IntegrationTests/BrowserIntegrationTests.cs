using System.Net;
using System.Net.Http.Json;

namespace TCMD.IntegrationTests;

[Collection(DatabaseIntegrationCollection.Name)]
public sealed class BrowserIntegrationTests(TcmdApiFactory factory)
{
    [Fact]
    public async Task RootAndRepresentativeAssets_AreServed_WhileUnknownApiRemainsProblemDetails()
    {
        using var client = factory.CreateClient();
        using var root = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, root.StatusCode);
        Assert.Equal("text/html", root.Content.Headers.ContentType?.MediaType);
        var html = await root.Content.ReadAsStringAsync();
        Assert.Contains("<div id=\"app\"></div>", html);
        Assert.Contains("<script type=\"module\" src=\"/js/app.js\"></script>", html);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/css/app.css")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/js/app.js")).StatusCode);
        using var missing = await client.GetAsync("/api/not-a-route");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("application/problem+json", missing.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Session_ReturnsSafeIdentity_AndInstructorLinkState()
    {
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/auth/session")).StatusCode);
        using var staff = await AuthenticatedClient.CreateAsync(factory, "Staff");
        var staffSession = await staff.GetFromJsonAsync<SessionDto>("/api/auth/session");
        Assert.Equal("Staff", staffSession!.Role);
        Assert.Equal("NotApplicable", staffSession.InstructorLinkStatus);
        Assert.Null(staffSession.InstructorId);
        using var instructor = await AuthenticatedClient.CreateAsync(factory, "Instructor");
        var instructorSession = await instructor.GetFromJsonAsync<SessionDto>("/api/auth/session");
        Assert.Equal("Unlinked", instructorSession!.InstructorLinkStatus);
    }

    [Fact]
    public async Task Antiforgery_RejectsMissingAndInvalidTokens_AndAcceptsValidToken()
    {
        var (userName, password) = await AuthenticatedClient.CreateAccountAsync(factory, "Staff", true);
        using var missing = await AuthenticatedClient.CreateWithAntiforgeryAsync(factory);
        Assert.Equal("https", missing.BaseAddress!.Scheme);
        missing.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest,
            (await missing.PostAsJsonAsync("/api/auth/login", new { userName, password })).StatusCode);

        using var invalid = await AuthenticatedClient.CreateWithAntiforgeryAsync(factory);
        invalid.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        invalid.DefaultRequestHeaders.Add("X-CSRF-TOKEN", "invalid");
        Assert.Equal(HttpStatusCode.BadRequest,
            (await invalid.PostAsJsonAsync("/api/auth/login", new { userName, password })).StatusCode);

        using var valid = await AuthenticatedClient.CreateWithAntiforgeryAsync(factory);
        Assert.Equal(HttpStatusCode.NoContent,
            (await valid.PostAsJsonAsync("/api/auth/login", new { userName, password })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await valid.GetAsync("/api/auth/session")).StatusCode);
    }

    private sealed record SessionDto(Guid Id, string UserName, string DisplayName, string Role,
        Guid? InstructorId, string InstructorLinkStatus);
}
