using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;

namespace TCMD.IntegrationTests;

[Collection(DatabaseIntegrationCollection.Name)]
public sealed class InstructorEndpointTests(TcmdApiFactory factory)
{
    [Theory]
    [InlineData("Administrator")]
    [InlineData("Staff")]
    public async Task OperationalRoles_CanCreateRetrieveUpdateAndDeactivate(string role)
    {
        using var client = await AuthenticatedClient.CreateAsync(factory, role);
        var created = await CreateAsync(client, "  Ada Lovelace  ", "  +212600000000  ", "  ada@example.com  ");
        Assert.Equal("Ada Lovelace", created.FullName);
        Assert.Equal("+212600000000", created.PhoneNumber);
        Assert.Equal("ada@example.com", created.Email);
        Assert.True(created.IsActive);
        Assert.NotEmpty(created.RowVersion);

        var retrieved = await client.GetFromJsonAsync<InstructorResponse>($"/api/instructors/{created.Id}");
        Assert.Equal(created, retrieved);

        var updated = await UpdateAsync(client, created, "Updated Ada", null, " ");
        Assert.Equal(created.Id, updated.Id);
        Assert.Equal(created.CreatedAtUtc, updated.CreatedAtUtc);
        Assert.Null(updated.PhoneNumber);
        Assert.Null(updated.Email);
        Assert.True(updated.LastUpdatedAtUtc > created.LastUpdatedAtUtc);
        Assert.NotEqual(created.RowVersion, updated.RowVersion);

        var inactive = await DeactivateAsync(client, updated.Id, updated.RowVersion);
        Assert.False(inactive.IsActive);
    }

    [Fact]
    public async Task ListSearchAndFilters_CoverAllContactFieldsAndEmptyResults()
    {
        using var client = await AuthenticatedClient.CreateAsync(factory, "Staff");
        var marker = Guid.NewGuid().ToString("N");
        var active = await CreateAsync(client, $"Name {marker}", $"+212{marker[..12]}", $"mail-{marker}@example.com");
        var inactive = await CreateAsync(client, $"Inactive {marker}", $"+213{marker[..12]}", $"old-{marker}@example.com");
        inactive = await DeactivateAsync(client, inactive.Id, inactive.RowVersion);

        foreach (var term in new[] { active.FullName, active.PhoneNumber!, active.Email! })
            Assert.Contains((await client.GetFromJsonAsync<List<InstructorResponse>>($"/api/instructors?search={Uri.EscapeDataString(term)}"))!, x => x.Id == active.Id);

        var all = (await client.GetFromJsonAsync<List<InstructorResponse>>("/api/instructors"))!;
        Assert.Contains(all, x => x.Id == active.Id); Assert.Contains(all, x => x.Id == inactive.Id);
        Assert.DoesNotContain((await client.GetFromJsonAsync<List<InstructorResponse>>("/api/instructors?isActive=true"))!, x => x.Id == inactive.Id);
        Assert.Contains((await client.GetFromJsonAsync<List<InstructorResponse>>("/api/instructors?isActive=false"))!, x => x.Id == inactive.Id);
        Assert.Empty((await client.GetFromJsonAsync<List<InstructorResponse>>($"/api/instructors?search={Guid.NewGuid():N}"))!);
    }

    [Fact]
    public async Task StaleWrites_ReturnConflictAndPreserveNewerData()
    {
        using var client = await AuthenticatedClient.CreateAsync(factory, "Staff");
        var original = await CreateAsync(client);
        var newer = await UpdateAsync(client, original, "Newer", "phone", "newer@example.com");
        using var staleUpdate = await client.PutAsJsonAsync($"/api/instructors/{original.Id}", new { fullName = "Stale", phoneNumber = "stale", email = "stale@example.com", rowVersion = original.RowVersion });
        using var staleDeactivate = await client.PostAsJsonAsync($"/api/instructors/{original.Id}/deactivate", new { rowVersion = original.RowVersion });
        Assert.Equal(HttpStatusCode.Conflict, staleUpdate.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, staleDeactivate.StatusCode);
        Assert.Equal(newer, await client.GetFromJsonAsync<InstructorResponse>($"/api/instructors/{original.Id}"));
    }

    [Fact]
    public async Task RepeatedDeactivation_IsIdempotentAndInactiveDetailsRemainEditable()
    {
        using var client = await AuthenticatedClient.CreateAsync(factory, "Staff");
        var created = await CreateAsync(client);
        var first = await DeactivateAsync(client, created.Id, created.RowVersion);
        var repeated = await DeactivateAsync(client, created.Id, created.RowVersion);
        Assert.Equal(first.RowVersion, repeated.RowVersion);
        Assert.Equal(first.LastUpdatedAtUtc, repeated.LastUpdatedAtUtc);
        var corrected = await UpdateAsync(client, repeated, "Corrected", null, null);
        Assert.False(corrected.IsActive);
    }

    [Fact]
    public async Task ValidationAndUnknownIds_ReturnProblemDetails()
    {
        using var client = await AuthenticatedClient.CreateAsync(factory);
        using var invalid = await client.PostAsJsonAsync("/api/instructors", new { fullName = " ", phoneNumber = new string('1', 51), email = "bad" });
        var validation = await invalid.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal("application/problem+json", invalid.Content.Headers.ContentType?.MediaType);
        Assert.Contains("fullName", validation!.Errors); Assert.Contains("phoneNumber", validation.Errors); Assert.Contains("email", validation.Errors);
        Assert.True(validation.Extensions.ContainsKey("traceId"));

        using var missing = await client.GetAsync($"/api/instructors/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        var rowVersion = Convert.ToBase64String(new byte[8]);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"/api/instructors/{Guid.NewGuid()}", new { fullName = "X", phoneNumber = (string?)null, email = (string?)null, rowVersion })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync($"/api/instructors/{Guid.NewGuid()}/deactivate", new { rowVersion })).StatusCode);
    }

    [Fact]
    public async Task AnonymousAndInstructor_AreRejectedForEveryEndpoint()
    {
        using var staff = await AuthenticatedClient.CreateAsync(factory, "Staff");
        var existing = await CreateAsync(staff);
        using var anonymous = await AuthenticatedClient.CreateWithAntiforgeryAsync(factory);
        using var instructor = await AuthenticatedClient.CreateAsync(factory, "Instructor");
        foreach (var (client, status) in new[] { (anonymous, HttpStatusCode.Unauthorized), (instructor, HttpStatusCode.Forbidden) })
        {
            Assert.Equal(status, (await client.PostAsJsonAsync("/api/instructors", new { fullName = "Blocked" })).StatusCode);
            Assert.Equal(status, (await client.GetAsync("/api/instructors")).StatusCode);
            Assert.Equal(status, (await client.GetAsync($"/api/instructors/{existing.Id}")).StatusCode);
            Assert.Equal(status, (await client.PutAsJsonAsync($"/api/instructors/{existing.Id}", new { fullName = "Blocked", rowVersion = existing.RowVersion })).StatusCode);
            Assert.Equal(status, (await client.PostAsJsonAsync($"/api/instructors/{existing.Id}/deactivate", new { rowVersion = existing.RowVersion })).StatusCode);
        }
    }

    private static async Task<InstructorResponse> CreateAsync(HttpClient client, string? name = null, string? phone = null, string? email = null)
    {
        using var response = await client.PostAsJsonAsync("/api/instructors", new { fullName = name ?? $"Instructor {Guid.NewGuid():N}", phoneNumber = phone, email });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var value = (await response.Content.ReadFromJsonAsync<InstructorResponse>())!;
        Assert.Equal($"/api/instructors/{value.Id}", response.Headers.Location?.OriginalString);
        return value;
    }

    private static async Task<InstructorResponse> UpdateAsync(HttpClient client, InstructorResponse value, string name, string? phone, string? email)
    {
        using var response = await client.PutAsJsonAsync($"/api/instructors/{value.Id}", new { fullName = name, phoneNumber = phone, email, rowVersion = value.RowVersion });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<InstructorResponse>())!;
    }

    private static async Task<InstructorResponse> DeactivateAsync(HttpClient client, Guid id, string rowVersion)
    {
        using var response = await client.PostAsJsonAsync($"/api/instructors/{id}/deactivate", new { rowVersion });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<InstructorResponse>())!;
    }

    private sealed record InstructorResponse(Guid Id, string FullName, string? PhoneNumber, string? Email, bool IsActive,
        DateTimeOffset CreatedAtUtc, DateTimeOffset LastUpdatedAtUtc, string RowVersion);
}
