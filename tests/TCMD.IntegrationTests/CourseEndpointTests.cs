using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;

namespace TCMD.IntegrationTests;

[Collection(DatabaseIntegrationCollection.Name)]
public sealed class CourseEndpointTests(TcmdApiFactory factory)
{
    [Theory]
    [InlineData("Administrator")]
    [InlineData("Staff")]
    public async Task OperationalRoles_CanCreateRetrieveUpdateAndDeactivate(string role)
    {
        using var client = await AuthenticatedClient.CreateAsync(factory, role);
        var created = await CreateAsync(client, "  cs-101/a.b_c  ", "  C# Basics  ", "  Introduction  ");
        Assert.Equal("CS-101/A.B_C", created.Code);
        Assert.Equal("C# Basics", created.Name);
        Assert.Equal("Introduction", created.Description);
        Assert.True(created.IsActive);
        Assert.NotEmpty(created.RowVersion);
        Assert.Equal(created, await client.GetFromJsonAsync<CourseResponse>($"/api/courses/{created.Id}"));

        var updated = await UpdateAsync(client, created, $"next-{Guid.NewGuid():N}", "Advanced", " ");
        Assert.Equal("Advanced", updated.Name);
        Assert.Null(updated.Description);
        Assert.Equal(created.CreatedAtUtc, updated.CreatedAtUtc);
        Assert.True(updated.LastUpdatedAtUtc > created.LastUpdatedAtUtc);
        Assert.NotEqual(created.RowVersion, updated.RowVersion);

        var inactive = await DeactivateAsync(client, updated.Id, updated.RowVersion);
        Assert.False(inactive.IsActive);
    }

    [Fact]
    public async Task ListSearchAndFilters_CoverCourseFieldsAndOrderByCode()
    {
        using var client = await AuthenticatedClient.CreateAsync(factory, "Staff");
        var marker = Guid.NewGuid().ToString("N");
        var later = await CreateAsync(client, $"Z-{marker}", $"Name {marker}", $"Description {marker}");
        var earlier = await CreateAsync(client, $"A-{marker}", $"Other {marker}", $"Other description {marker}");
        var inactive = await DeactivateAsync(client, later.Id, later.RowVersion);

        foreach (var term in new[] { earlier.Code.ToLowerInvariant(), earlier.Name, earlier.Description! })
            Assert.Contains((await client.GetFromJsonAsync<List<CourseResponse>>($"/api/courses?search={Uri.EscapeDataString(term)}"))!, x => x.Id == earlier.Id);

        var matching = (await client.GetFromJsonAsync<List<CourseResponse>>($"/api/courses?search={marker}"))!;
        Assert.True(matching.FindIndex(x => x.Id == earlier.Id) < matching.FindIndex(x => x.Id == later.Id));
        Assert.Contains((await client.GetFromJsonAsync<List<CourseResponse>>("/api/courses"))!, x => x.Id == inactive.Id);
        Assert.DoesNotContain((await client.GetFromJsonAsync<List<CourseResponse>>("/api/courses?isActive=true"))!, x => x.Id == inactive.Id);
        Assert.Contains((await client.GetFromJsonAsync<List<CourseResponse>>("/api/courses?isActive=false"))!, x => x.Id == inactive.Id);
        Assert.Empty((await client.GetFromJsonAsync<List<CourseResponse>>($"/api/courses?search={Guid.NewGuid():N}"))!);
    }

    [Fact]
    public async Task DuplicateCodes_AreRejectedAcrossCasingAndInactiveRecords()
    {
        using var client = await AuthenticatedClient.CreateAsync(factory, "Staff");
        var code = $"course-{Guid.NewGuid():N}";
        var existing = await CreateAsync(client, code, "Original", null);
        existing = await DeactivateAsync(client, existing.Id, existing.RowVersion);

        using var duplicateCreate = await client.PostAsJsonAsync("/api/courses", new { code = $"  {code.ToUpperInvariant()}  ", name = "Duplicate" });
        Assert.Equal(HttpStatusCode.Conflict, duplicateCreate.StatusCode);
        Assert.Equal("Course code is already in use.", (await duplicateCreate.Content.ReadFromJsonAsync<ProblemDetails>())?.Title);

        var other = await CreateAsync(client, $"other-{Guid.NewGuid():N}", "Other", null);
        using var duplicateUpdate = await client.PutAsJsonAsync($"/api/courses/{other.Id}", new
        { code, name = other.Name, description = other.Description, rowVersion = other.RowVersion });
        Assert.Equal(HttpStatusCode.Conflict, duplicateUpdate.StatusCode);
        Assert.Equal(other, await client.GetFromJsonAsync<CourseResponse>($"/api/courses/{other.Id}"));
    }

    [Fact]
    public async Task ConcurrentCreates_CannotBypassCodeUniqueness()
    {
        using var firstClient = await AuthenticatedClient.CreateAsync(factory, "Staff");
        using var secondClient = await AuthenticatedClient.CreateAsync(factory, "Staff");
        var code = $"race-{Guid.NewGuid():N}";

        var responses = await Task.WhenAll(
            firstClient.PostAsJsonAsync("/api/courses", new { code, name = "First" }),
            secondClient.PostAsJsonAsync("/api/courses", new { code = code.ToUpperInvariant(), name = "Second" }));
        using var firstResponse = responses[0];
        using var secondResponse = responses[1];

        Assert.Contains(responses, response => response.StatusCode == HttpStatusCode.Created);
        Assert.Contains(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        var stored = await firstClient.GetFromJsonAsync<List<CourseResponse>>($"/api/courses?search={code}");
        Assert.Single(stored!);
    }

    [Fact]
    public async Task ValidationAndUnknownIds_ReturnProblemDetails()
    {
        using var client = await AuthenticatedClient.CreateAsync(factory, "Staff");
        using var invalid = await client.PostAsJsonAsync("/api/courses", new
        { code = "bad@code", name = " ", description = new string('x', 2001) });
        var validation = await invalid.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal("application/problem+json", invalid.Content.Headers.ContentType?.MediaType);
        Assert.Contains("code", validation!.Errors);
        Assert.Contains("name", validation.Errors);
        Assert.Contains("description", validation.Errors);
        Assert.True(validation.Extensions.ContainsKey("traceId"));

        using var tooLong = await client.PostAsJsonAsync("/api/courses", new { code = new string('A', 51), name = new string('N', 201) });
        var lengthErrors = await tooLong.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Contains("code", lengthErrors!.Errors);
        Assert.Contains("name", lengthErrors.Errors);

        var missingId = Guid.NewGuid();
        var rowVersion = Convert.ToBase64String(new byte[8]);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/courses/{missingId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"/api/courses/{missingId}", new { code = "CODE", name = "Name", rowVersion })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync($"/api/courses/{missingId}/deactivate", new { rowVersion })).StatusCode);

        var existing = await CreateAsync(client);
        using var badVersion = await client.PutAsJsonAsync($"/api/courses/{existing.Id}", new { code = existing.Code, name = existing.Name, rowVersion = Convert.ToBase64String(new byte[7]) });
        Assert.Equal(HttpStatusCode.BadRequest, badVersion.StatusCode);
    }

    [Fact]
    public async Task StaleWrites_ReturnConflictAndPreserveNewerData()
    {
        using var client = await AuthenticatedClient.CreateAsync(factory, "Staff");
        var original = await CreateAsync(client);
        var newer = await UpdateAsync(client, original, original.Code, "Newer", "Newer description");
        using var staleUpdate = await client.PutAsJsonAsync($"/api/courses/{original.Id}", new
        { code = original.Code, name = "Stale", description = "Stale", rowVersion = original.RowVersion });
        using var staleDeactivate = await client.PostAsJsonAsync($"/api/courses/{original.Id}/deactivate", new { rowVersion = original.RowVersion });
        Assert.Equal(HttpStatusCode.Conflict, staleUpdate.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, staleDeactivate.StatusCode);
        Assert.Equal(newer, await client.GetFromJsonAsync<CourseResponse>($"/api/courses/{original.Id}"));
    }

    [Fact]
    public async Task RepeatedDeactivationIsIdempotentAndInactiveDetailsRemainEditable()
    {
        using var client = await AuthenticatedClient.CreateAsync(factory, "Staff");
        var created = await CreateAsync(client);
        var first = await DeactivateAsync(client, created.Id, created.RowVersion);
        var repeated = await DeactivateAsync(client, created.Id, created.RowVersion);
        Assert.Equal(first.RowVersion, repeated.RowVersion);
        Assert.Equal(first.LastUpdatedAtUtc, repeated.LastUpdatedAtUtc);
        var corrected = await UpdateAsync(client, repeated, repeated.Code, "Corrected", null);
        Assert.False(corrected.IsActive);
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
            Assert.Equal(status, (await client.PostAsJsonAsync("/api/courses", new { code = "BLOCKED", name = "Blocked" })).StatusCode);
            Assert.Equal(status, (await client.GetAsync("/api/courses")).StatusCode);
            Assert.Equal(status, (await client.GetAsync($"/api/courses/{existing.Id}")).StatusCode);
            Assert.Equal(status, (await client.PutAsJsonAsync($"/api/courses/{existing.Id}", new { code = existing.Code, name = "Blocked", rowVersion = existing.RowVersion })).StatusCode);
            Assert.Equal(status, (await client.PostAsJsonAsync($"/api/courses/{existing.Id}/deactivate", new { rowVersion = existing.RowVersion })).StatusCode);
        }
        Assert.Equal(existing, await staff.GetFromJsonAsync<CourseResponse>($"/api/courses/{existing.Id}"));
    }

    private static async Task<CourseResponse> CreateAsync(HttpClient client, string? code = null, string? name = null, string? description = null)
    {
        using var response = await client.PostAsJsonAsync("/api/courses", new
        { code = code ?? $"C-{Guid.NewGuid():N}", name = name ?? "Course", description });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var course = (await response.Content.ReadFromJsonAsync<CourseResponse>())!;
        Assert.Equal($"/api/courses/{course.Id}", response.Headers.Location?.OriginalString);
        return course;
    }

    private static async Task<CourseResponse> UpdateAsync(HttpClient client, CourseResponse course, string code, string name, string? description)
    {
        using var response = await client.PutAsJsonAsync($"/api/courses/{course.Id}", new { code, name, description, rowVersion = course.RowVersion });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CourseResponse>())!;
    }

    private static async Task<CourseResponse> DeactivateAsync(HttpClient client, Guid id, string rowVersion)
    {
        using var response = await client.PostAsJsonAsync($"/api/courses/{id}/deactivate", new { rowVersion });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CourseResponse>())!;
    }

    private sealed record CourseResponse(Guid Id, string Code, string Name, string? Description, bool IsActive,
        DateTimeOffset CreatedAtUtc, DateTimeOffset LastUpdatedAtUtc, string RowVersion);
}
