using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TCMD.Domain.Courses;
using TCMD.Domain.Attendance;
using TCMD.Domain.Enrollments;
using TCMD.Domain.Instructors;
using TCMD.Domain.Students;
using TCMD.Domain.TrainingGroups;
using TCMD.Domain.TrainingSessions;
using TCMD.Infrastructure.Identity;

namespace TCMD.Infrastructure.Persistence;

public sealed class TcmdDbContext(DbContextOptions<TcmdDbContext> options)
    : IdentityDbContext<StaffUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<Instructor> Instructors => Set<Instructor>();
    public DbSet<TrainingGroup> TrainingGroups => Set<TrainingGroup>();
    public DbSet<TrainingSession> TrainingSessions => Set<TrainingSession>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasSequence<long>("StudentNumberSequence", "dbo")
            .StartsAt(1)
            .IncrementsBy(1);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TcmdDbContext).Assembly);
    }
}
