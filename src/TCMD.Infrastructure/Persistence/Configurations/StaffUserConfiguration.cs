using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TCMD.Infrastructure.Identity;

namespace TCMD.Infrastructure.Persistence.Configurations;

internal sealed class StaffUserConfiguration : IEntityTypeConfiguration<StaffUser>
{
    public void Configure(EntityTypeBuilder<StaffUser> builder)
    {
        builder.HasIndex(user => user.InstructorId).IsUnique().HasFilter("[InstructorId] IS NOT NULL");
        builder.HasOne<TCMD.Domain.Instructors.Instructor>().WithMany().HasForeignKey(user => user.InstructorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
