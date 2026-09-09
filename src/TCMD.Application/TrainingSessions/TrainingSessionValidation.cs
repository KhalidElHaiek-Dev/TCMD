using TCMD.Domain.TrainingGroups;
using TCMD.Domain.TrainingSessions;

namespace TCMD.Application.TrainingSessions;

public static class TrainingSessionValidation
{
    public static bool HasExpectedVersion(TrainingSession session, byte[] expected) =>
        session.RowVersion is not null && session.RowVersion.AsSpan().SequenceEqual(expected);

    public static bool IsEligible(TrainingSessionGroupReference group) =>
        group.Status is TrainingGroupStatus.Planned or TrainingGroupStatus.Active;

    public static bool IsWithinRange(TrainingSessionGroupReference group, DateOnly date) =>
        date >= group.PlannedStartDate && date <= group.PlannedEndDate;
}
