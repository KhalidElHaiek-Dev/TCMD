using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TCMD.Application.Courses;
using TCMD.Application.Instructors;
using TCMD.Application.Students;
using TCMD.Application.StaffAccounts;
using TCMD.Application.TrainingGroups;
using TCMD.Infrastructure.Identity;
using TCMD.Infrastructure.Courses;
using TCMD.Infrastructure.Instructors;
using TCMD.Infrastructure.Persistence;
using TCMD.Infrastructure.Students;
using TCMD.Infrastructure.TrainingGroups;

namespace TCMD.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<TcmdDbContext>(options => options.UseSqlServer(connectionString));
        services.AddHealthChecks().AddDbContextCheck<TcmdDbContext>("database");
        services.AddScoped<ICourseStore, EfCourseStore>();
        services.AddScoped<IStudentStore, EfStudentStore>();
        services.AddScoped<IInstructorStore, EfInstructorStore>();
        services.AddScoped<ITrainingGroupStore, EfTrainingGroupStore>();
        services.AddScoped<IStudentNumberGenerator, SqlStudentNumberGenerator>();
        services.AddScoped<IStaffAccountStore, IdentityStaffAccountStore>();
        return services;
    }
}
