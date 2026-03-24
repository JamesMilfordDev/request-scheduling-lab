// A simple data holder with no methods.
// An instance represents a request waiting to be admitted.
// Created by the BoundedSjfScheduler when a request must wait.

// Unlike the Waiter class, a BoundedSjfWaiter is mutable: its
// SkippedOverCount can be adjusted.

using System.Threading.Tasks;

namespace RequestSchedulingLab;
public class BoundedSjfWaiter
{
    public TaskCompletionSource Tcs {get;}
    public int RequestedDuration {get;}
    public int SequenceNumber {get;}
    public int SkippedOverCount {get; set;}
    public BoundedSjfWaiter(TaskCompletionSource tcs, int requestedDuration, 
        int sequenceNumber, int skippedOverCount)
    {
        this.Tcs = tcs;
        this.RequestedDuration = requestedDuration;
        this.SequenceNumber = sequenceNumber;
        this.SkippedOverCount = skippedOverCount;
    }
}