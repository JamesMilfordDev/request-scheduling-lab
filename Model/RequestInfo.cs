// An immutable simple data holder with no methods.
// An instance is created and associated with the HttpContext
// of a request when that request enters the middleware pipeline.
// The instance represents what the request is.

namespace RequestSchedulingLab;
public class RequestInfo
{
    public string RequestId {get;}
    public string Path {get;}
    public int RequestedDuration {get;}

    public PriorityLevel Priority {get;} 
    public RequestInfo(string requestId, string path, int requestedDuration, PriorityLevel priority)
    {
        this.RequestId = requestId;
        this.Path = path;
        this.RequestedDuration = requestedDuration;
        this.Priority = priority;
    }
}