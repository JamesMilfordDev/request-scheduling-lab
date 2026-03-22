// An immutable simple data holder with no methods.
// Each instance represents a snapshot of the state of
// a scheduler.

namespace RequestSchedulingLab;
public class SchedulerSnapshot
{
    public bool HasActiveRequest {get;}
    public int WaitingCount {get;}

    public SchedulerSnapshot(bool hasActiveRequest, int waitingCount)
    {
        this.HasActiveRequest = hasActiveRequest;
        this.WaitingCount = waitingCount;
    }
}