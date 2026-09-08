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
        using var client = await AuthenticatedClient.CreateAsync(factory);
        var created = await RegisterStudentAsync(client);
        using var getResponse = await client.GetAsync($"/api/students/{created.Id}");
        var retrieved = await getResponse.Content.ReadFromJsonAsync<StudentResponse>();

        Assert.Matches("^STU-[0-9]{6}$", created.StudentNumber);
        Assert.NotEmpty(created.RowVersion);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Equal(created, retrieved);
    }

    [Theory]
    [InlineData("Administrator")]
    [InlineData("Staff")]
    public async Task AuthorizedOperationalRoles_CanListStudents(string role)
    {
        using var client = await AuthenticatedClient.CreateAsync(factory, role);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/students")).StatusCode);
    }

    [Fact]
    public async Task ListAndSearch_ReturnMatchesAcrossEveryFieldAndFiltersActiveStatus()
    {
        using var client = await AuthenticatedClient.CreateAsync(factory, "Staff");
        var marker = Guid.NewGuid().ToString("N");
        var active = await RegisterStudentAsync(client, $"Search {marker}", $"+212{marker[..12]}", $"search-{marker}@example.com");
        var inactive = await RegisterStudentAsync(client, $"Inactive {marker}", $"+213{marker[..12]}", $"inactive-{marker}@example.com");
        inactive = await DeactivateAsync(client, inactive.Id, inactive.RowVersion);

        foreach (var search in new[] { active.StudentNumber, marker, active.PhoneNumber, active.Email! })
        {
            var matches = await client.GetFromJsonAsync<List<StudentResponse>>($"/api/students?search={Uri.EscapeDataString(search)}");
            Assert.Contains(matches!, student => student.Id == active.Id);
        }

        var all = await client.GetFromJsonAsync<List<StudentResponse>>("/api/students");
        Assert.NotNull(all);
        Assert.Contains(all, student => student.Id == active.Id);
        Assert.Contains(all, student => student.Id == inactive.Id);

        var activeOnly = await client.GetFromJsonAsync<List<StudentResponse>>("/api/students?isActive=true");
        Assert.NotNull(activeOnly);
        Assert.Contains(activeOnly, student => student.Id == active.Id);
        Assert.DoesNotContain(activeOnly, student => student.Id == inactive.Id);

        var inactiveOnly = await client.GetFromJsonAsync<List<StudentResponse>>("/api/students?isActive=false");
        Assert.NotNull(inactiveOnly);
        Assert.Contains(inactiveOnly, student => student.Id == inactive.Id);
        Assert.DoesNotContain(inactiveOnly, student => student.Id == active.Id);

        var empty = await client.GetFromJsonAsync<List<StudentResponse>>($"/api/students?search={Guid.NewGuid():N}");
        Assert.Empty(empty!);
    }

    [Fact]
    public async Task Update_WithValidData_PreservesImmutableFieldsAndReturnsNewRowVersion()
    {
        using var administrator = await AuthenticatedClient.CreateAsync(factory, "Administrator");
        var student = await RegisterStudentAsync(administrator);
        using var response = await administrator.PutAsJsonAsync($"/api/students/{student.Id}", new
        {
            fullName = "  Updated Student  ",
            phoneNumber = "  +212611111111  ",
            email = "  updated@example.com  ",
            rowVersion = student.RowVersion
        });
        var updated = await response.Content.ReadFromJsonAsync<StudentResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(updated);
        Assert.Equal(student.Id, updated.Id);
        Assert.Equal(student.StudentNumber, updated.StudentNumber);
        Assert.Equal(student.CreatedAtUtc, updated.CreatedAtUtc);
        Assert.Equal("Updated Student", updated.FullName);
        Assert.Equal("+212611111111", updated.PhoneNumber);
        Assert.Equal("updated@example.com", updated.Email);
        Assert.True(updated.LastUpdatedAtUtc > student.LastUpdatedAtUtc);
        Assert.NotEqual(student.RowVersion, updated.RowVersion);
    }

    [Fact]
    public async Task UpdateAndDeactivate_RejectInvalidOrMissingStudents()
    {
        using var client = await AuthenticatedClient.CreateAsync(factory, "Staff");
        var student = await RegisterStudentAsync(client);
        using var invalid = await client.PutAsJsonAsync($"/api/students/{student.Id}", new
        {
            fullName = " ", phoneNumber = "", email = "not-an-email", rowVersion = Convert.ToBase64String(new byte[7])
        });
        var validation = await invalid.Content.ReadFromJsonAsync<ValidationProblemDetails>();

        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.NotNull(validation);
        Assert.Contains("fullName", validation.Errors);
        Assert.Contains("phoneNumber", validation.Errors);
        Assert.Contains("email", validation.Errors);
        Assert.Contains("rowVersion", validation.Errors);

        var rowVersion = Convert.ToBase64String(new byte[8]);
        using var updateMissing = await client.PutAsJsonAsync($"/api/students/{Guid.NewGuid()}", new
        {
            fullName = "Unknown", phoneNumber = "+212600000000", email = (string?)null, rowVersion
        });
        using var deactivateMissing = await client.PostAsJsonAsync($"/api/students/{Guid.NewGuid()}/deactivate", new { rowVersion });
        Assert.Equal(HttpStatusCode.NotFound, updateMissing.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deactivateMissing.StatusCode);
    }

    [Fact]
    public async Task Deactivate_IsPersistedAndRepeatedRequestDoesNotChangeVersionOrTimestamp()
    {
        using var staff = await AuthenticatedClient.CreateAsync(factory, "Staff");
        var student = await RegisterStudentAsync(staff);

        var deactivated = await DeactivateAsync(staff, student.Id, student.RowVersion);
        var repeated = await DeactivateAsync(staff, student.Id, student.RowVersion);
        var retrieved = await staff.GetFromJsonAsync<StudentResponse>($"/api/students/{student.Id}");

        Assert.False(deactivated.IsActive);
        Assert.Equal(deactivated.RowVersion, repeated.RowVersion);
        Assert.Equal(deactivated.LastUpdatedAtUtc, repeated.LastUpdatedAtUtc);
        Assert.Equal(deactivated, retrieved);
    }

    [Fact]
    public async Task Update_WithStaleRowVersion_ReturnsConflictAndPreservesNewerData()
    {
        using var staff = await AuthenticatedClient.CreateAsync(factory, "Staff");
        var student = await RegisterStudentAsync(staff);
        var first = await UpdateAsync(staff, student, "Newer Name", "+212622222222");
        using var stale = await staff.PutAsJsonAsync($"/api/students/{student.Id}", new
        {
            fullName = "Stale Name", phoneNumber = "+212633333333", email = "stale@example.com", rowVersion = student.RowVersion
        });
        var problem = await stale.Content.ReadFromJsonAsync<ProblemDetails>();
        using var staleDeactivation = await staff.PostAsJsonAsync(
            $"/api/students/{student.Id}/deactivate",
            new { rowVersion = student.RowVersion });
        var retrieved = await staff.GetFromJsonAsync<StudentResponse>($"/api/students/{student.Id}");

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, staleDeactivation.StatusCode);
        Assert.NotNull(problem);
        Assert.True(problem.Extensions.ContainsKey("traceId"));
        Assert.Equal(first, retrieved);
    }

    [Fact]
    public async Task NewStudentOperations_RejectAnonymousAndInstructorWithoutChangingData()
    {
        using var staff = await AuthenticatedClient.CreateAsync(factory, "Staff");
        var student = await RegisterStudentAsync(staff);
        using var anonymous = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        using var instructor = await AuthenticatedClient.CreateAsync(factory, "Instructor");

        foreach (var (client, expectedStatus) in new[]
        {
            (anonymous, HttpStatusCode.Unauthorized),
            (instructor, HttpStatusCode.Forbidden)
        })
        {
            Assert.Equal(expectedStatus, (await client.GetAsync("/api/students")).StatusCode);
            Assert.Equal(expectedStatus, (await client.PutAsJsonAsync($"/api/students/{student.Id}", new
            {
                fullName = "Blocked", phoneNumber = "+212600000000", email = (string?)null, rowVersion = student.RowVersion
            })).StatusCode);
            Assert.Equal(expectedStatus, (await client.PostAsJsonAsync(
                $"/api/students/{student.Id}/deactivate", new { rowVersion = student.RowVersion })).StatusCode);
        }

        Assert.Equal(student, await staff.GetFromJsonAsync<StudentResponse>($"/api/students/{student.Id}"));
    }

    [Fact]
    public async Task Register_WithMissingRequiredData_ReturnsValidationProblemDetails()
    {
        using var client = await AuthenticatedClient.CreateAsync(factory);
        using var response = await client.PostAsJsonAsync("/api/students", new { fullName = " ", phoneNumber = "" });
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
        using var client = await AuthenticatedClient.CreateAsync(factory);
        using var response = await client.GetAsync($"/api/students/{Guid.NewGuid()}");
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Student not found", problem?.Title);
        Assert.True(problem?.Extensions.ContainsKey("traceId"));
    }

    private static async Task<StudentResponse> RegisterStudentAsync(HttpClient client, string? fullName = null, string? phoneNumber = null, string? email = null)
    {
        var marker = Guid.NewGuid().ToString("N");
        using var response = await client.PostAsJsonAsync("/api/students", new
        {
            fullName = fullName ?? $"Student {marker}",
            phoneNumber = phoneNumber ?? $"+212{marker[..12]}",
            email = email ?? $"student-{marker}@example.com"
        });
        var student = await response.Content.ReadFromJsonAsync<StudentResponse>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(student);
        Assert.Equal($"/api/students/{student.Id}", response.Headers.Location?.OriginalString);
        return student;
    }

    private static async Task<StudentResponse> UpdateAsync(HttpClient client, StudentResponse student, string fullName, string phoneNumber)
    {
        using var response = await client.PutAsJsonAsync($"/api/students/{student.Id}", new
        {
            fullName, phoneNumber, email = "newer@example.com", rowVersion = student.RowVersion
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<StudentResponse>())!;
    }

    private static async Task<StudentResponse> DeactivateAsync(HttpClient client, Guid id, string rowVersion)
    {
        using var response = await client.PostAsJsonAsync($"/api/students/{id}/deactivate", new { rowVersion });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<StudentResponse>())!;
    }

    private sealed record StudentResponse(
        Guid Id,
        string StudentNumber,
        string FullName,
        string PhoneNumber,
        string? Email,
        bool IsActive,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset LastUpdatedAtUtc,
        string RowVersion);
}
