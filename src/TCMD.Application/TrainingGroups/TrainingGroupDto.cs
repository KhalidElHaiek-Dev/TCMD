using TCMD.Domain.TrainingGroups;

namespace TCMD.Application.TrainingGroups;

public sealed record TrainingGroupDto(Guid Id, string Name, Guid CourseId, Guid? PrimaryInstructorId,
    DateOnly PlannedStartDate, DateOnly PlannedEndDate, TrainingGroupStatus Status,
    DateTimeOffset CreatedAtUtc, DateTimeOffset LastUpdatedAtUtc, byte[] RowVersion)
{
    public static TrainingGroupDto From(TrainingGroup group) => new(group.Id, group.Name, group.CourseId,
        group.PrimaryInstructorId, group.PlannedStartDate, group.PlannedEndDate, group.Status,
        group.CreatedAtUtc, group.LastUpdatedAtUtc, group.RowVersion ?? []);
}
