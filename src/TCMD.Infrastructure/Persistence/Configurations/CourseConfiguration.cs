using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TCMD.Domain.Courses;

namespace TCMD.Infrastructure.Persistence.Configurations;

internal sealed class CourseConfiguration : IEntityTypeConfiguration<Course>
{
    public void Configure(EntityTypeBuilder<Course> builder)
    {
        builder.ToTable("Courses");
        builder.HasKey(course => course.Id);
        builder.Property(course => course.Code).HasMaxLength(50).IsRequired();
        builder.HasIndex(course => course.Code).IsUnique();
        builder.Property(course => course.Name).HasMaxLength(200).IsRequired();
        builder.Property(course => course.Description).HasMaxLength(2000);
        builder.Property(course => course.IsActive).IsRequired();
        builder.Property(course => course.CreatedAtUtc).IsRequired();
        builder.Property(course => course.LastUpdatedAtUtc).IsRequired();
        builder.Property(course => course.RowVersion).IsRowVersion();
    }
}
