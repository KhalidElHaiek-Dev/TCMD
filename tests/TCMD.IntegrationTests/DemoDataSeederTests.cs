using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TCMD.Api.Development;
using TCMD.Domain.Attendance;
using TCMD.Domain.Enrollments;
using TCMD.Domain.TrainingGroups;
using TCMD.Domain.TrainingSessions;
using TCMD.Infrastructure.Persistence;

namespace TCMD.IntegrationTests;

[Collection(DatabaseIntegrationCollection.Name)]
public sealed class DemoDataSeederTests(TcmdApiFactory factory) : IAsyncLifetime
{
    public Task InitializeAsync() => ClearAsync();
    public Task DisposeAsync() => ClearAsync();

    [Fact]
    public async Task Seed_creates_coherent_screenshot_data_and_refuses_reseeding()
    {
        using var scope = factory.Services.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<DemoDataSeeder>();
        var db = scope.ServiceProvider.GetRequiredService<TcmdDbContext>();

        await seeder.SeedAsync();

        Assert.Equal(10, await db.Students.CountAsync());
        Assert.Equal(4, await db.Instructors.CountAsync());
        Assert.Equal(5, await db.Courses.CountAsync());
        Assert.Equal(5, await db.TrainingGroups.CountAsync());
        Assert.Equal(3, await db.Users.CountAsync());
        Assert.Single(await db.TrainingGroups.Where(group =>
            group.Status == TrainingGroupStatus.Planned && group.PrimaryInstructorId == null).ToListAsync());

        var instructorAccount = await db.Users.SingleAsync(user => user.UserName == DemoDataSeeder.InstructorUserName);
        Assert.NotNull(instructorAccount.InstructorId);

        var amiraId = await db.Students.Where(student => student.FullName == "Amira El Mansouri")
            .Select(student => student.Id).SingleAsync();
        Assert.Equal(2, await db.Enrollments.CountAsync(enrollment => enrollment.StudentId == amiraId));

        var screenshotGroup = await db.TrainingGroups
            .SingleAsync(group => group.Name == "Digital Operations — Autumn Cohort");
        var variedSession = await db.TrainingSessions
            .Where(session => session.TrainingGroupId == screenshotGroup.Id &&
                session.Status == TrainingSessionStatus.Completed)
            .OrderBy(session => session.SessionDate)
            .ThenBy(session => session.StartTime)
            .Select(session => session.Id)
            .FirstAsync();
        var statuses = await db.AttendanceRecords.Where(record => record.TrainingSessionId == variedSession)
            .Select(record => record.Status).Distinct().ToListAsync();
        Assert.Contains(AttendanceStatus.Present, statuses);
        Assert.Contains(AttendanceStatus.Absent, statuses);
        Assert.Contains(AttendanceStatus.Late, statuses);
        Assert.Contains(AttendanceStatus.Excused, statuses);
        Assert.True(await db.Enrollments.AnyAsync(enrollment =>
            enrollment.TrainingGroupId == screenshotGroup.Id && enrollment.Status == EnrollmentStatus.Active &&
            !db.AttendanceRecords.Any(record => record.TrainingSessionId == variedSession &&
                record.EnrollmentId == enrollment.Id)));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => seeder.SeedAsync());
        Assert.Contains("not empty", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(10, await db.Students.CountAsync());
    }

    private async Task ClearAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TcmdDbContext>();
        await db.AttendanceRecords.ExecuteDeleteAsync();
        await db.TrainingSessions.ExecuteDeleteAsync();
        await db.Enrollments.ExecuteDeleteAsync();
        await db.TrainingGroups.ExecuteDeleteAsync();
        await db.UserRoles.ExecuteDeleteAsync();
        await db.Users.ExecuteDeleteAsync();
        await db.Roles.ExecuteDeleteAsync();
        await db.Students.ExecuteDeleteAsync();
        await db.Instructors.ExecuteDeleteAsync();
        await db.Courses.ExecuteDeleteAsync();
    }
}

public sealed class DemoSeedSafetyTests
{
    [Fact]
    public void Safety_rejects_production_and_non_demo_databases()
    {
        Assert.Throws<InvalidOperationException>(() => DemoSeedSafety.EnsureAllowed("Production", "TCMD.Demo"));
        Assert.Throws<InvalidOperationException>(() => DemoSeedSafety.EnsureAllowed("Development", "TCMD.Development"));
        DemoSeedSafety.EnsureAllowed("Development", "TCMD.Demo");
    }
}
