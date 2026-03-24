// A request scheduler that selects the next request based on priority, then
// requested duration:
// High > Medium > Low.
// For ties in priority: shortest requested duration.
// For ties in shortest requested duration: FIFO.

using System.Collections.Generic;
using System.Threading.Tasks;

namespace RequestSchedulingLab;
public class PrioritySjfScheduler : IScheduler
{
    private bool HasActiveRequest {get; set;}

    // As with SjfScheduler, we represent each waiting request with a custom
    // Waiter class instance. Like PriorityScheduler, we have three separate
    // data structures that we store Waiter instances in, one for each priority
    // level (High, Medium, Low). Like SjfScheduler, these data structures are
    // List objects.

    private List<Waiter> HighWaitingList {get;}
    private List<Waiter> MediumWaitingList {get;}
    private List<Waiter> LowWaitingList {get;}

    // As with SjfScheduler, to tie-break RequestedDuration with FIFO, we
    // assign waiting requests a SequenceNumber. As such, our scheduler
    // holds a private tracker for the NextSequenceNumber:

    private int NextSequenceNumber {get; set;}

    private object Lock {get;}

    // The WaitingCount of the Snapshot is the sum of the Count properties of
    // our three List<Waiter> objects.
    public SchedulerSnapshot Snapshot
    {
        get
        {
            lock (this.Lock)
            {
                return new SchedulerSnapshot(
                    this.HasActiveRequest, 
                    this.HighWaitingList.Count + this.MediumWaitingList.Count
                    + this.LowWaitingList.Count);
            }
        }
    }
    
    public PrioritySjfScheduler()
        {
                this.HasActiveRequest = false;
                this.HighWaitingList = new List<Waiter>();
                this.MediumWaitingList = new List<Waiter>();
                this.LowWaitingList = new List<Waiter>();
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

            // Otherwise, wait asynchronously.

            // As with SjfScheduler, we create a Waiter instance:

            Waiter waiter = new Waiter(
                new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously),
                requestInfo.RequestedDuration,
                this.NextSequenceNumber
                );

            // Adjust NextSequenceNumber:

            this.NextSequenceNumber++;

            // Add waiter to the relevant List<Waiter> object:

            if (requestInfo.Priority == PriorityLevel.High)
            {
                this.HighWaitingList.Add(waiter);
            }
            else if (requestInfo.Priority == PriorityLevel.Medium)
            {
                this.MediumWaitingList.Add(waiter);
            }
            else
            {
                this.LowWaitingList.Add(waiter);
            }
            
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
            if (this.HighWaitingList.Count > 0)
            {
                foreach (Waiter waiter in this.HighWaitingList)
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
                this.HighWaitingList.Remove(next!);
            }
            else if (this.MediumWaitingList.Count > 0)
            {
                foreach (Waiter waiter in this.MediumWaitingList)
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
                this.MediumWaitingList.Remove(next!);
            }
            else if (this.LowWaitingList.Count > 0)
            {
                foreach (Waiter waiter in this.LowWaitingList)
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
                this.LowWaitingList.Remove(next!);
            }
            else
            {
                this.HasActiveRequest = false;
            }
        }
        
        if (next != null)
        {
            next.Tcs.SetResult();
        }
    }
}