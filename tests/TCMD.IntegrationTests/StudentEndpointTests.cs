using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;

namespace TCMD.IntegrationTests;

[Collection(DatabaseIntegrationCollection.Name)]
public sealed class StudentEndpointTests(TcmdApiFactory factory)
{
    [Fact]
    public async Task RegisterThenGet_WithValidData_ReturnsPersistedStudentAndGeneratedNumber()
    {
        using var client = factory.CreateClient();
        var uniquePhone = $"+2126{Random.Shared.NextInt64(100000000, 999999999)}";

        using var createResponse = await client.PostAsJsonAsync("/api/students", new
        {
            fullName = "Ada Lovelace",
            phoneNumber = uniquePhone,
            email = "ada@example.com"
        });
        var created = await createResponse.Content.ReadFromJsonAsync<StudentResponse>();

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotNull(created);
        Assert.Matches("^STU-[0-9]{6}$", created.StudentNumber);
        Assert.Equal($"/api/students/{created.Id}", createResponse.Headers.Location?.OriginalString);

        using var getResponse = await client.GetAsync($"/api/students/{created.Id}");
        var retrieved = await getResponse.Content.ReadFromJsonAsync<StudentResponse>();

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Equal(created, retrieved);
    }

    [Fact]
    public async Task Register_WithMissingRequiredData_ReturnsValidationProblemDetails()
    {
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/students", new
        {
            fullName = " ",
            phoneNumber = ""
        });
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.NotNull(problem);
        Assert.Contains("fullName", problem.Errors.Keys);
        Assert.Contains("phoneNumber", problem.Errors.Keys);
        Assert.True(problem.Extensions.ContainsKey("traceId"));
    }

    [Fact]
    public async Task Get_WhenStudentDoesNotExist_ReturnsProblemDetails()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"/api/students/{Guid.NewGuid()}");
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Student not found", problem?.Title);
        Assert.True(problem?.Extensions.ContainsKey("traceId"));
    }

    private sealed record StudentResponse(
        Guid Id,
        string StudentNumber,
        string FullName,
        string PhoneNumber,
        string? Email,
        bool IsActive,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset LastUpdatedAtUtc);
}
