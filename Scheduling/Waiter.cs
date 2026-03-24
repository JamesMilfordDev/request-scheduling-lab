// An immutable simple data holder with no methods.
// An instance represents a request waiting to be admitted.
// Created by certain schedulers when a request must wait.

// Simpler schedulers (FifoScheduler and PriorityScheduler)
// represent waiting requests directly with TaskCompletionSource instances.

using System.Threading.Tasks;

namespace RequestSchedulingLab;
public class Waiter
{
    public TaskCompletionSource Tcs {get;}
    public int RequestedDuration {get;}
    public int SequenceNumber {get;}
    public Waiter(TaskCompletionSource tcs, int requestedDuration, int sequenceNumber)
    {
        this.Tcs = tcs;
        this.RequestedDuration = requestedDuration;
        this.SequenceNumber = sequenceNumber;
    }
}