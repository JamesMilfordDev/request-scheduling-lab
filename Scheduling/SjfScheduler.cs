// A request scheduler that selects the next request based on requested duration:
// Shortest requested duration first.
// FIFO to break ties.

using System.Collections.Generic;
using System.Threading.Tasks;

namespace RequestSchedulingLab;
public class SjfScheduler : IScheduler
{
    private bool HasActiveRequest {get; set;}

    // In FifoScheduler and PriorityScheduler, we represented each waiting
    // request with a simple TaskCompletionSource instance, which was stored
    // in the appropriate Queue (one Queue for FifoScheduler, three Queues for
    // PriorityScheduler). These Queue structures themselves encoded the
    // scheduling policy. The scheduler then only needed to wake the requests
    // in dequeue order.

    // With an SJF system, our scheduler needs to compare waiting requests by
    // RequestedDuration, and then tie-break with FIFO. Representing a waiting
    // request with a TaskCompletionSource instance does not provide enough
    // information to achieve this.

    // As such, we use a custom Waiter class to represent a waiting request. We 
    // store these Waiter instances in a List<Waiter>. Note that selection 
    // amongst waiting requests is O(n) with a List object, but I am optimising
    // for clarity here (explicit selection logic), not high-volume throughput.

    private List<Waiter> WaitingList {get;}

    // To tie-break RequestedDuration with FIFO, we
    // assign waiting requests a SequenceNumber. As such, our scheduler
    // holds a private tracker for the NextSequenceNumber. For our purposes,
    // an int is sufficient:

    private int NextSequenceNumber {get; set;}

    private object Lock {get;}

    // The WaitingCount of the Snapshot is the Count of WaitingList:
    public SchedulerSnapshot Snapshot
    {
        get
        {
            lock (this.Lock)
            {
                return new SchedulerSnapshot(
                    this.HasActiveRequest, 
                    this.WaitingList.Count);
            }
        }
    }
    
    public SjfScheduler()
        {
                this.HasActiveRequest = false;
                this.WaitingList = new List<Waiter>();
                this.NextSequenceNumber = 0;
                this.Lock = new object();
        }


    // WaitForTurnAsync() handles a request as it reaches the scheduler. It decides
    // whether the request may be admitted immediately or must wait asynchronously:
    public Task WaitForTurnAsync(RequestInfo requestInfo)
    {
        lock (this.Lock)
        {
            // Immediate admission if no request is being processed.

            if (!this.HasActiveRequest)
            {
                this.HasActiveRequest = true;
                return Task.CompletedTask;   
            }

            // Otherwise, wait asynchronously based on RequestedDuration (then FIFO).

            // Unlike FifoScheduler and PriorityScheduler, we here create a 
            // Waiter instance:

            Waiter waiter = new Waiter(
                new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously),
                requestInfo.RequestedDuration,
                this.NextSequenceNumber
                );

            // Adjust NextSequenceNumber:

            this.NextSequenceNumber++;

            // Add waiter to WaitingList:

            this.WaitingList.Add(waiter);
            
            return waiter.Tcs.Task;
        }
    }

    // Release() handles what happens when a request has been fully processed:
    public void Release()
    {
        // We decide on the next Waiter instance:

        Waiter? next = null;
        int lowestRequestedDuration = int.MaxValue;
        int lowestSequenceNumber = int.MaxValue;

        lock (this.Lock)
        {
            if (this.WaitingList.Count == 0)
            {
                this.HasActiveRequest = false;
            }
            else
            {
                foreach (Waiter waiter in this.WaitingList)
                {
                    if (waiter.RequestedDuration < lowestRequestedDuration)
                    {
                        next = waiter;
                        lowestRequestedDuration = waiter.RequestedDuration;
                        lowestSequenceNumber = waiter.SequenceNumber;
                    }
                    
                    else if (waiter.RequestedDuration == lowestRequestedDuration)
                    {
                        if (waiter.SequenceNumber < lowestSequenceNumber)
                        {
                            next = waiter;
                            lowestSequenceNumber = waiter.SequenceNumber;
                        }
                    }
                }
                this.WaitingList.Remove(next!);
            }
        }

        if (next != null)
        {
            next.Tcs.SetResult();
        }
    }
}