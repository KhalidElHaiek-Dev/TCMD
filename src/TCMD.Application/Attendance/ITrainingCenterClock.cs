namespace TCMD.Application.Attendance;

public interface ITrainingCenterClock
{
    DateTimeOffset GetUtcNow();
    DateTime GetLocalNow();
}
