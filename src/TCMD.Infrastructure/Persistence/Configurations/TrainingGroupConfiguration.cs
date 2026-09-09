using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TCMD.Domain.TrainingGroups;

namespace TCMD.Infrastructure.Persistence.Configurations;

internal sealed class TrainingGroupConfiguration : IEntityTypeConfiguration<TrainingGroup>
{
    public void Configure(EntityTypeBuilder<TrainingGroup> builder)
    {
        builder.ToTable("TrainingGroups");
        builder.HasKey(group => group.Id);
        builder.Property(group => group.Name).HasMaxLength(200).IsRequired();
        builder.Property(group => group.PlannedStartDate).HasColumnType("date").IsRequired();
        builder.Property(group => group.PlannedEndDate).HasColumnType("date").IsRequired();
        builder.Property(group => group.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(group => group.CreatedAtUtc).IsRequired();
        builder.Property(group => group.LastUpdatedAtUtc).IsRequired();
        builder.Property(group => group.RowVersion).IsRowVersion();
        builder.HasIndex(group => new { group.CourseId, group.Name, group.PlannedStartDate }).IsUnique();
        builder.HasIndex(group => group.Status);
        builder.HasIndex(group => group.PrimaryInstructorId);
        builder.HasOne<TCMD.Domain.Courses.Course>().WithMany().HasForeignKey(group => group.CourseId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TCMD.Domain.Instructors.Instructor>().WithMany().HasForeignKey(group => group.PrimaryInstructorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
