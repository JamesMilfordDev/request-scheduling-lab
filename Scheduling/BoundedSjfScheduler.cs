// A request scheduler that selects the next request based on requested duration,
// with a fairness guarantee built in:

// If a request has a SkippedOverCount greater than or equal to the
// MaxSkippedOverCount, choose the request with the greatest SkippedOverCount.
// If there are ties in SkippedOverCount, select according to FIFO.

// If no request has a SkippedOverCount greater than or equal to the
// MaxSkippedOverCount, choose the request with the lowest requested duration.
// If there are ties in requested duration, select according to FIFO.


using System.Collections.Generic;
using System.Threading.Tasks;

namespace RequestSchedulingLab;
public class BoundedSjfScheduler : IScheduler
{
    private bool HasActiveRequest {get; set;}

    // Unlike our previous schedulers, we represent a waiting request with a
    // BoundedSjfWaiter instance. As with SjfScheduler, we store these 
    // representations in a simple List object:

    private List<BoundedSjfWaiter> WaitingList {get;}

    // To tie-break with FIFO when required, we
    // assign waiting requests a SequenceNumber. As such, our scheduler
    // holds a private tracker for the NextSequenceNumber. For our purposes,
    // an int is sufficient:

    private int NextSequenceNumber {get; set;}

    // We set the SkippedOverCount boundary:

    private int MaxSkippedOverCount {get;}

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
    
    public BoundedSjfScheduler()
        {
                this.HasActiveRequest = false;
                this.WaitingList = new List<BoundedSjfWaiter>();
                this.NextSequenceNumber = 0;

                // A small MaxSkippedOverCount value is used to make the fairness
                // mechanism observable in test runs:
                this.MaxSkippedOverCount = 3;

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

            // We create a BoundedSjfWaiter instance:

            BoundedSjfWaiter waiter = new BoundedSjfWaiter(
                new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously),
                requestInfo.RequestedDuration,
                this.NextSequenceNumber,
                0
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
        // We decide on the next BoundedSjfWaiter instance:

        BoundedSjfWaiter? next = null;
        int lowestRequestedDuration = int.MaxValue;
        int lowestSequenceNumber = int.MaxValue;
        int greatestSkippedOverCount = 0;

        lock (this.Lock)
        {
            if (this.WaitingList.Count == 0)
            {
                this.HasActiveRequest = false;
            }
            else
            {
                // Check fairness override selection:

                foreach (BoundedSjfWaiter waiter in this.WaitingList)
                {
                    if (waiter.SkippedOverCount >= this.MaxSkippedOverCount)
                    {
                        if (waiter.SkippedOverCount > greatestSkippedOverCount)
                        {
                            next = waiter;
                            lowestRequestedDuration = waiter.RequestedDuration;
                            lowestSequenceNumber = waiter.SequenceNumber;
                            greatestSkippedOverCount = waiter.SkippedOverCount;
                        }
                        else if (waiter.SkippedOverCount == greatestSkippedOverCount)
                        {
                            if (waiter.SequenceNumber < lowestSequenceNumber)
                            {
                                next = waiter;
                                lowestRequestedDuration = waiter.RequestedDuration;
                                lowestSequenceNumber = waiter.SequenceNumber;
                            }
                        }
                    }
                }

                // If nothing selected, use SJF selection:

                if (next == null)
                {
                    foreach (BoundedSjfWaiter waiter in this.WaitingList)
                    {
                        if (waiter.RequestedDuration < lowestRequestedDuration)
                        {
                            next = waiter;
                            lowestRequestedDuration = waiter.RequestedDuration;
                            lowestSequenceNumber = waiter.SequenceNumber;

                            // There is no need to update greatestSkippedOverCount
                            // anymore.
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
                }

            // Remove the selected BoundedSjfWaiter:

            this.WaitingList.Remove(next!);

            // Increment the SkippedOverCount of all remaining BoundedSjfWaiter objects:

            foreach (BoundedSjfWaiter waiter in this.WaitingList)
                {
                    waiter.SkippedOverCount++;
                }
            }
        }

        if (next != null)
        {
            next.Tcs.SetResult();
        }
    }
}