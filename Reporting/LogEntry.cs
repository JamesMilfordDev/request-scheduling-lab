// An immutable simple data holder with no methods.
// Each instance represents an event in the lifecycle of a 
// request (arrival, admission, completion). Instances are
// created in middleware and added to the singleton Log instance.

namespace RequestSchedulingLab;
public class LogEntry
{
    public DateTime Timestamp {get;}
    public string RequestId {get;}
    public string Path {get;}
    public int RequestedDuration {get;}
    public PriorityLevel Priority {get;}
    public EventType Event {get;}
    public bool IsRequestRunning {get;}
    public int WaitingCount {get;}

    public LogEntry(DateTime timestamp, string requestId, string path, 
        int requestedDuration, PriorityLevel priority, EventType eventType, 
        bool isRequestRunning, int waitingCount)
    {
        this.Timestamp = timestamp;
        this.RequestId = requestId;
        this.Path = path;
        this.RequestedDuration = requestedDuration;
        this.Priority = priority;
        this.Event = eventType;
        this.IsRequestRunning = isRequestRunning;
        this.WaitingCount = waitingCount;
    }
}
