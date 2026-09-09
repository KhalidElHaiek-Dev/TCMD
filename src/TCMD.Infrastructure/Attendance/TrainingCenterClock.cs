using TCMD.Application.Attendance;

namespace TCMD.Infrastructure.Attendance;

internal sealed class TrainingCenterClock(TimeProvider timeProvider, TimeZoneInfo timeZone) : ITrainingCenterClock
{
    public DateTimeOffset GetUtcNow() => timeProvider.GetUtcNow();
    public DateTime GetLocalNow() => TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), timeZone).DateTime;
}
