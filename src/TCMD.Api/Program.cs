using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using TCMD.Api.Authentication;
using TCMD.Api.Courses;
using TCMD.Api.Enrollments;
using TCMD.Api.Instructors;
using TCMD.Api.Students;
using TCMD.Api.StaffAccounts;
using TCMD.Api.TrainingGroups;
using TCMD.Api.TrainingSessions;
using TCMD.Api.Attendance;
using TCMD.Application.Attendance;
using TCMD.Application.Students;
using TCMD.Application.Courses;
using TCMD.Application.Enrollments;
using TCMD.Application.Instructors;
using TCMD.Application.StaffAccounts;
using TCMD.Application.TrainingGroups;
using TCMD.Application.TrainingSessions;
using System.Text.Json.Serialization;
using TCMD.Infrastructure;
using TCMD.Infrastructure.Identity;
using TCMD.Infrastructure.Persistence;
using TCMD.Api.Development;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("TCMD")
    ?? throw new InvalidOperationException(
        "Connection string 'TCMD' is not configured. Set ConnectionStrings__TCMD through user-secrets or an environment variable.");

builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
        context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
});
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "TCMD.Antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
});
builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<RecordAttendance>();
builder.Services.AddScoped<CorrectAttendance>();
builder.Services.AddScoped<ListTrainingSessionAttendance>();
builder.Services.AddScoped<ListStudentAttendance>();
builder.Services.AddScoped<ListTrainingGroupAttendance>();
builder.Services.AddScoped<CreateCourse>();
builder.Services.AddScoped<CreateEnrollment>();
builder.Services.AddScoped<CompleteEnrollment>();
builder.Services.AddScoped<WithdrawEnrollment>();
builder.Services.AddScoped<ReactivateEnrollment>();
builder.Services.AddScoped<ListTrainingGroupEnrollments>();
builder.Services.AddScoped<ListStudentEnrollments>();
builder.Services.AddScoped<GetCourseById>();
builder.Services.AddScoped<SearchCourses>();
builder.Services.AddScoped<UpdateCourse>();
builder.Services.AddScoped<DeactivateCourse>();
builder.Services.AddScoped<RegisterStudent>();
builder.Services.AddScoped<GetStudentById>();
builder.Services.AddScoped<SearchStudents>();
builder.Services.AddScoped<UpdateStudent>();
builder.Services.AddScoped<DeactivateStudent>();
builder.Services.AddScoped<CreateInstructor>();
builder.Services.AddScoped<GetInstructorById>();
builder.Services.AddScoped<SearchInstructors>();
builder.Services.AddScoped<UpdateInstructor>();
builder.Services.AddScoped<DeactivateInstructor>();
builder.Services.AddScoped<CreateTrainingGroup>();
builder.Services.AddScoped<GetTrainingGroupById>();
builder.Services.AddScoped<SearchTrainingGroups>();
builder.Services.AddScoped<UpdateTrainingGroup>();
builder.Services.AddScoped<ActivateTrainingGroup>();
builder.Services.AddScoped<CompleteTrainingGroup>();
builder.Services.AddScoped<CancelTrainingGroup>();
builder.Services.AddScoped<CreateTrainingSession>();
builder.Services.AddScoped<GetTrainingSessionById>();
builder.Services.AddScoped<ListTrainingGroupSessions>();
builder.Services.AddScoped<UpdateTrainingSession>();
builder.Services.AddScoped<CompleteTrainingSession>();
builder.Services.AddScoped<CancelTrainingSession>();
builder.Services.AddScoped<StaffAccountAdministration>();
builder.Services.AddScoped<RequestAccessResolver>();
builder.Services.AddScoped<DemoDataSeeder>();
var trainingCenterTimeZone = builder.Configuration["TrainingCenter:TimeZone"]
    ?? throw new InvalidOperationException("TrainingCenter:TimeZone is not configured.");
builder.Services.AddInfrastructure(connectionString, trainingCenterTimeZone);
builder.Services.AddHttpContextAccessor();
builder.Services.AddIdentityCore<StaffUser>(options =>
{
    options.Password.RequiredLength = 8;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Lockout.AllowedForNewUsers = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
})
.AddRoles<IdentityRole<Guid>>()
.AddEntityFrameworkStores<TcmdDbContext>()
.AddDefaultTokenProviders()
.AddSignInManager();
builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
});
builder.Services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.Zero);
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser().Build();
    options.AddPolicy(TcmdPolicies.OperationalStaff, policy => policy.RequireRole("Administrator", "Staff"));
    options.AddPolicy(TcmdPolicies.Administrators, policy => policy.RequireRole("Administrator"));
    options.AddPolicy(TcmdPolicies.OperationalStaffOrInstructor,
        policy => policy.RequireRole("Administrator", "Staff", "Instructor"));
});

var app = builder.Build();

if (args.Contains("--seed-demo", StringComparer.Ordinal))
{
    await DemoSeedCommand.RunAsync(app.Services, app.Environment);
    return;
}

await BootstrapAdministrator.InitializeAsync(app.Services, app.Configuration);

app.UseExceptionHandler();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseStatusCodePages(async statusCodeContext =>
{
    await Results.Problem(statusCode: statusCodeContext.HttpContext.Response.StatusCode)
        .ExecuteAsync(statusCodeContext.HttpContext);
});
app.UseAuthentication();
app.UseAntiforgery();
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api")
        && !HttpMethods.IsGet(context.Request.Method)
        && !HttpMethods.IsHead(context.Request.Method)
        && !HttpMethods.IsOptions(context.Request.Method))
    {
        try
        {
            await context.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(context);
        }
        catch (AntiforgeryValidationException)
        {
            await Results.Problem(statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid antiforgery token",
                detail: "Refresh the page and try again.").ExecuteAsync(context);
            return;
        }
    }

    await next(context);
});
app.UseAuthorization();
app.MapStaticAssets().AllowAnonymous();
app.MapGet("/", () => Results.Redirect("/index.html")).AllowAnonymous();
app.MapOpenApi().AllowAnonymous();
app.MapHealthChecks("/health", new HealthCheckOptions { ResponseWriter = WriteHealthResponseAsync }).AllowAnonymous();
app.MapAuthenticationEndpoints();
app.MapCourseEndpoints();
app.MapEnrollmentEndpoints();
app.MapStudentEndpoints();
app.MapInstructorEndpoints();
app.MapTrainingGroupEndpoints();
app.MapTrainingSessionEndpoints();
app.MapAttendanceEndpoints();
app.MapStaffAccountEndpoints();
app.MapFallback(() => Results.NotFound()).AllowAnonymous();

app.Run();

static Task WriteHealthResponseAsync(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json";
    return context.Response.WriteAsJsonAsync(new
    {
        status = report.Status.ToString(),
        checks = report.Entries.ToDictionary(
            entry => entry.Key,
            entry => new { status = entry.Value.Status.ToString() })
    });
}

public partial class Program;
