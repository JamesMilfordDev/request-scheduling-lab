// IScheduler defines the contract for request schedulers.
// A scheduler determines when incoming requests are admitted for processing,
// based on request metadata and the current system state.

namespace RequestSchedulingLab;
public interface IScheduler
{
    SchedulerSnapshot Snapshot {get;}

    Task WaitForTurnAsync(RequestInfo requestInfo);

    void Release();
}