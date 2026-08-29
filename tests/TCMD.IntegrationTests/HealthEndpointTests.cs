using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace TCMD.IntegrationTests;

[Collection(DatabaseIntegrationCollection.Name)]
public sealed class HealthEndpointTests(TcmdApiFactory factory)
{
    [Fact]
    public async Task GetHealth_WhenApiAndDatabaseAreAvailable_ReturnsHealthy()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health");
        var health = await response.Content.ReadFromJsonAsync<HealthResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(health);
        Assert.Equal("Healthy", health.Status);
        Assert.Equal("Healthy", health.Checks["database"].Status);

        using var openApiResponse = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, openApiResponse.StatusCode);

        using var notFoundResponse = await client.GetAsync("/not-an-endpoint");
        var problem = await notFoundResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(HttpStatusCode.NotFound, notFoundResponse.StatusCode);
        Assert.Equal("application/problem+json", notFoundResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal(StatusCodes.Status404NotFound, problem?.Status);
        Assert.True(problem?.Extensions.ContainsKey("traceId"));
    }

    private sealed record HealthResponse(string Status, Dictionary<string, HealthCheckResponse> Checks);

    private sealed record HealthCheckResponse(string Status);
}
