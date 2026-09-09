using TCMD.Domain.TrainingGroups;

namespace TCMD.Application.TrainingGroups;

public enum TrainingGroupMutationStatus
{
    Success, NotFound, CourseNotFound, CourseInactive, InstructorNotFound, InstructorInactive,
    InstructorRequired, ProhibitedChange, SessionOutsideProposedDateRange, InvalidStatusTransition,
    ConcurrencyConflict, DuplicateGroup
}

public sealed record TrainingGroupMutationResult(TrainingGroupMutationStatus Status, TrainingGroupDto? Group)
{
    public static TrainingGroupMutationResult Success(TrainingGroup group) => new(TrainingGroupMutationStatus.Success, TrainingGroupDto.From(group));
    public static TrainingGroupMutationResult Error(TrainingGroupMutationStatus status) => new(status, null);
    public static TrainingGroupMutationResult FromStore(TrainingGroupStoreSaveStatus status, TrainingGroup group) => status switch
    {
        TrainingGroupStoreSaveStatus.Success => Success(group),
        TrainingGroupStoreSaveStatus.ConcurrencyConflict => Error(TrainingGroupMutationStatus.ConcurrencyConflict),
        TrainingGroupStoreSaveStatus.DuplicateGroup => Error(TrainingGroupMutationStatus.DuplicateGroup),
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };
}
