using TCMD.Domain.Enrollments;

namespace TCMD.Application.Enrollments;

public abstract class ChangeEnrollmentStatus(IEnrollmentStore store, TimeProvider timeProvider)
{
    protected async Task<EnrollmentMutationResult> ChangeAsync(Guid id, byte[] rowVersion, EnrollmentStatus target,
        CancellationToken cancellationToken)
    {
        var enrollment = await store.GetForUpdateAsync(id, cancellationToken);
        if (enrollment is null) return EnrollmentMutationResult.Error(EnrollmentMutationStatus.EnrollmentNotFound);
        if (!EnrollmentValidation.HasExpectedVersion(enrollment, rowVersion))
            return EnrollmentMutationResult.Error(EnrollmentMutationStatus.ConcurrencyConflict);

        if (target == EnrollmentStatus.Active && enrollment.Status == EnrollmentStatus.Withdrawn)
        {
            var error = await EnrollmentValidation.ValidateStudentAsync(store, enrollment.StudentId, cancellationToken)
                ?? await EnrollmentValidation.ValidateGroupAsync(store, enrollment.TrainingGroupId, cancellationToken);
            if (error is not null) return EnrollmentMutationResult.Error(error.Value);
        }

        bool changed;
        try
        {
            var now = timeProvider.GetUtcNow();
            changed = target switch
            {
                EnrollmentStatus.Active => enrollment.Reactivate(now),
                EnrollmentStatus.Completed => enrollment.Complete(now),
                EnrollmentStatus.Withdrawn => enrollment.Withdraw(now),
                _ => throw new ArgumentOutOfRangeException(nameof(target))
            };
        }
        catch (InvalidOperationException)
        {
            return EnrollmentMutationResult.Error(EnrollmentMutationStatus.InvalidStatusTransition);
        }

        return changed
            ? EnrollmentMutationResult.FromStore(await store.SaveAsync(enrollment, rowVersion, cancellationToken), enrollment)
            : EnrollmentMutationResult.Success(enrollment);
    }
}

public sealed class CompleteEnrollment(IEnrollmentStore store, TimeProvider timeProvider)
    : ChangeEnrollmentStatus(store, timeProvider)
{
    public Task<EnrollmentMutationResult> ExecuteAsync(Guid id, byte[] rowVersion, CancellationToken cancellationToken) =>
        ChangeAsync(id, rowVersion, EnrollmentStatus.Completed, cancellationToken);
}

public sealed class WithdrawEnrollment(IEnrollmentStore store, TimeProvider timeProvider)
    : ChangeEnrollmentStatus(store, timeProvider)
{
    public Task<EnrollmentMutationResult> ExecuteAsync(Guid id, byte[] rowVersion, CancellationToken cancellationToken) =>
        ChangeAsync(id, rowVersion, EnrollmentStatus.Withdrawn, cancellationToken);
}

public sealed class ReactivateEnrollment(IEnrollmentStore store, TimeProvider timeProvider)
    : ChangeEnrollmentStatus(store, timeProvider)
{
    public Task<EnrollmentMutationResult> ExecuteAsync(Guid id, byte[] rowVersion, CancellationToken cancellationToken) =>
        ChangeAsync(id, rowVersion, EnrollmentStatus.Active, cancellationToken);
}
