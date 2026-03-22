// An immutable simple data holder with no methods.
// Each instance is derived from the event log and 
// represents the lifecycle and performance metrics
// of a single completed request.

// All instances are created when the application stops running.

namespace RequestSchedulingLab;
public class RequestSummary
{
    // Request metadata:
    public string RequestId {get;}
    public string Path {get;}
    public int RequestedDuration {get;}
    public PriorityLevel Priority {get;} 

    // Timestamps:

    public DateTime ArrivedAt {get;}
    public DateTime AdmittedAt {get;}
    public DateTime CompletedAt {get;}

    // Derived metrics:

    public int WaitingTimeMs {get;}
    public int ServiceTimeMs {get;}
    public int TotalTimeMs {get;}

    public RequestSummary(string requestId, string path, int requestedDuration,
        PriorityLevel priority, DateTime arrivedAt, DateTime admittedAt, 
        DateTime completedAt)
    {
        this.RequestId = requestId;
        this.Path = path;
        this.RequestedDuration = requestedDuration;
        this.Priority = priority;
        this.ArrivedAt = arrivedAt;
        this.AdmittedAt = admittedAt;
        this.CompletedAt = completedAt;

        this.WaitingTimeMs = (int)(this.AdmittedAt - this.ArrivedAt).TotalMilliseconds;
        this.ServiceTimeMs = (int)(this.CompletedAt - this.AdmittedAt).TotalMilliseconds;
        this.TotalTimeMs   = (int)(this.CompletedAt - this.ArrivedAt).TotalMilliseconds;
    }

}
