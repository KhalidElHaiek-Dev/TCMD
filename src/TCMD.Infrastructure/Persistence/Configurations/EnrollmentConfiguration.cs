using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TCMD.Domain.Enrollments;

namespace TCMD.Infrastructure.Persistence.Configurations;

internal sealed class EnrollmentConfiguration : IEntityTypeConfiguration<Enrollment>
{
    public void Configure(EntityTypeBuilder<Enrollment> builder)
    {
        builder.ToTable("Enrollments");
        builder.HasKey(enrollment => enrollment.Id);
        builder.Property(enrollment => enrollment.EnrollmentDate).HasColumnType("date").IsRequired();
        builder.Property(enrollment => enrollment.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(enrollment => enrollment.CreatedAtUtc).IsRequired();
        builder.Property(enrollment => enrollment.LastUpdatedAtUtc).IsRequired();
        builder.Property(enrollment => enrollment.RowVersion).IsRowVersion();
        builder.HasIndex(enrollment => new { enrollment.StudentId, enrollment.TrainingGroupId }).IsUnique();
        builder.HasIndex(enrollment => new { enrollment.TrainingGroupId, enrollment.Status });
        builder.HasIndex(enrollment => new { enrollment.StudentId, enrollment.Status });
        builder.HasOne<TCMD.Domain.Students.Student>().WithMany().HasForeignKey(enrollment => enrollment.StudentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TCMD.Domain.TrainingGroups.TrainingGroup>().WithMany()
            .HasForeignKey(enrollment => enrollment.TrainingGroupId).OnDelete(DeleteBehavior.Restrict);
    }
}
