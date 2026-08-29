using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TCMD.Application.Students;
using TCMD.Infrastructure.Persistence;
using TCMD.Infrastructure.Students;

namespace TCMD.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<TcmdDbContext>(options => options.UseSqlServer(connectionString));
        services.AddHealthChecks().AddDbContextCheck<TcmdDbContext>("database");
        services.AddScoped<IStudentStore, EfStudentStore>();
        services.AddScoped<IStudentNumberGenerator, SqlStudentNumberGenerator>();
        return services;
    }
}
