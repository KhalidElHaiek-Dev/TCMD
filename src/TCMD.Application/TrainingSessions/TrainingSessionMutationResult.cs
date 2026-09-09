using TCMD.Domain.TrainingSessions;

namespace TCMD.Application.TrainingSessions;

public enum TrainingSessionMutationStatus
{
    Success, SessionNotFound, TrainingGroupNotFound, TrainingGroupIneligible,
    SessionDateOutsideGroupRange, TerminalSession, InvalidStatusTransition, ConcurrencyConflict
}

public sealed record TrainingSessionMutationResult(TrainingSessionMutationStatus Status,
    TrainingSessionDto? Session)
{
    public static TrainingSessionMutationResult Success(TrainingSession session) =>
        new(TrainingSessionMutationStatus.Success, TrainingSessionDto.From(session));
    public static TrainingSessionMutationResult Error(TrainingSessionMutationStatus status) => new(status, null);
    public static TrainingSessionMutationResult FromStore(TrainingSessionStoreSaveStatus status,
        TrainingSession session) => status switch
        {
            TrainingSessionStoreSaveStatus.Success => Success(session),
            TrainingSessionStoreSaveStatus.ConcurrencyConflict => Error(TrainingSessionMutationStatus.ConcurrencyConflict),
            _ => throw new ArgumentOutOfRangeException(nameof(status))
        };
}
