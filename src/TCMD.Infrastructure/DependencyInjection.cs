using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TCMD.Application.Courses;
using TCMD.Application.Attendance;
using TCMD.Application.Enrollments;
using TCMD.Application.Instructors;
using TCMD.Application.Students;
using TCMD.Application.StaffAccounts;
using TCMD.Application.TrainingGroups;
using TCMD.Application.TrainingSessions;
using TCMD.Infrastructure.Identity;
using TCMD.Infrastructure.Attendance;
using TCMD.Infrastructure.Courses;
using TCMD.Infrastructure.Enrollments;
using TCMD.Infrastructure.Instructors;
using TCMD.Infrastructure.Persistence;
using TCMD.Infrastructure.Students;
using TCMD.Infrastructure.TrainingGroups;
using TCMD.Infrastructure.TrainingSessions;

namespace TCMD.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString,
        string trainingCenterTimeZoneId)
    {
        services.AddDbContext<TcmdDbContext>(options => options.UseSqlServer(connectionString));
        services.AddHealthChecks().AddDbContextCheck<TcmdDbContext>("database");
        services.AddScoped<ICourseStore, EfCourseStore>();
        services.AddScoped<IAttendanceStore, EfAttendanceStore>();
        services.AddSingleton(TimeZoneInfo.FindSystemTimeZoneById(trainingCenterTimeZoneId));
        services.AddSingleton<ITrainingCenterClock, TrainingCenterClock>();
        services.AddScoped<IEnrollmentStore, EfEnrollmentStore>();
        services.AddScoped<IStudentStore, EfStudentStore>();
        services.AddScoped<IInstructorStore, EfInstructorStore>();
        services.AddScoped<ITrainingGroupStore, EfTrainingGroupStore>();
        services.AddScoped<ITrainingSessionStore, EfTrainingSessionStore>();
        services.AddScoped<IStudentNumberGenerator, SqlStudentNumberGenerator>();
        services.AddScoped<IStaffAccountStore, IdentityStaffAccountStore>();
        return services;
    }
}
