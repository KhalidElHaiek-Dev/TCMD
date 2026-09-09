using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TCMD.Domain.Attendance;
using TCMD.Infrastructure.Identity;

namespace TCMD.Infrastructure.Persistence.Configurations;

internal sealed class AttendanceRecordConfiguration : IEntityTypeConfiguration<AttendanceRecord>
{
    public void Configure(EntityTypeBuilder<AttendanceRecord> builder)
    {
        builder.ToTable("AttendanceRecords");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.CorrectionNote).HasMaxLength(AttendanceRecord.CorrectionNoteMaxLength);
        builder.Property(x => x.RecordedAtUtc).IsRequired();
        builder.Property(x => x.LastUpdatedAtUtc).IsRequired();
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasIndex(x => new { x.EnrollmentId, x.TrainingSessionId }).IsUnique()
            .HasDatabaseName("IX_AttendanceRecords_EnrollmentId_TrainingSessionId");
        builder.HasIndex(x => new { x.TrainingSessionId, x.EnrollmentId });
        builder.HasOne<Domain.Enrollments.Enrollment>().WithMany().HasForeignKey(x => x.EnrollmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Domain.TrainingSessions.TrainingSession>().WithMany().HasForeignKey(x => x.TrainingSessionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<StaffUser>().WithMany().HasForeignKey(x => x.CreatedByStaffUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<StaffUser>().WithMany().HasForeignKey(x => x.LastUpdatedByStaffUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
