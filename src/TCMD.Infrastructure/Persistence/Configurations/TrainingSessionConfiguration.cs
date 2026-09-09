using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TCMD.Domain.TrainingSessions;

namespace TCMD.Infrastructure.Persistence.Configurations;

internal sealed class TrainingSessionConfiguration : IEntityTypeConfiguration<TrainingSession>
{
    public void Configure(EntityTypeBuilder<TrainingSession> builder)
    {
        builder.ToTable("TrainingSessions", table =>
            table.HasCheckConstraint("CK_TrainingSessions_EndTimeAfterStartTime", "[EndTime] > [StartTime]"));
        builder.HasKey(session => session.Id);
        builder.Property(session => session.SessionDate).HasColumnType("date").IsRequired();
        builder.Property(session => session.StartTime).HasColumnType("time").IsRequired();
        builder.Property(session => session.EndTime).HasColumnType("time").IsRequired();
        builder.Property(session => session.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(session => session.Location).HasMaxLength(500);
        builder.Property(session => session.CreatedAtUtc).IsRequired();
        builder.Property(session => session.LastUpdatedAtUtc).IsRequired();
        builder.Property(session => session.RowVersion).IsRowVersion();
        builder.HasIndex(session => new { session.TrainingGroupId, session.SessionDate, session.StartTime });
        builder.HasOne<TCMD.Domain.TrainingGroups.TrainingGroup>().WithMany()
            .HasForeignKey(session => session.TrainingGroupId).OnDelete(DeleteBehavior.Restrict);
    }
}
