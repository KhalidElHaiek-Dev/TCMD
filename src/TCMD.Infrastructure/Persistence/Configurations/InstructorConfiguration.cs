using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TCMD.Domain.Instructors;

namespace TCMD.Infrastructure.Persistence.Configurations;

internal sealed class InstructorConfiguration : IEntityTypeConfiguration<Instructor>
{
    public void Configure(EntityTypeBuilder<Instructor> builder)
    {
        builder.ToTable("Instructors");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.FullName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.PhoneNumber).HasMaxLength(50);
        builder.Property(x => x.Email).HasMaxLength(320);
        builder.Property(x => x.IsActive).IsRequired();
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.LastUpdatedAtUtc).IsRequired();
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}
