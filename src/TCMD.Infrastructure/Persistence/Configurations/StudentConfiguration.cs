using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TCMD.Domain.Students;

namespace TCMD.Infrastructure.Persistence.Configurations;

internal sealed class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> builder)
    {
        builder.ToTable("Students");
        builder.HasKey(student => student.Id);
        builder.Property(student => student.StudentNumber).HasMaxLength(10).IsRequired();
        builder.HasIndex(student => student.StudentNumber).IsUnique();
        builder.Property(student => student.FullName).HasMaxLength(200).IsRequired();
        builder.Property(student => student.PhoneNumber).HasMaxLength(50).IsRequired();
        builder.Property(student => student.Email).HasMaxLength(320);
        builder.Property(student => student.IsActive).IsRequired();
        builder.Property(student => student.CreatedAtUtc).IsRequired();
        builder.Property(student => student.LastUpdatedAtUtc).IsRequired();
        builder.Property(student => student.RowVersion).IsRowVersion();
    }
}
