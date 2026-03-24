// A request scheduler that selects the next request based on priority:
// High > Medium > Low.
// FIFO within each priority level.

using System.Collections.Generic;
using System.Threading.Tasks;

namespace RequestSchedulingLab;
public class PriorityScheduler : IScheduler
{
    private bool HasActiveRequest {get; set;}

    // In FifoScheduler, we employed a single Queue<TaskCompletionSource>
    // instance to handle waiting requests. Now we employ three, one for each
    // priority:

    private Queue<TaskCompletionSource> HighWaitingQueue {get;}
    private Queue<TaskCompletionSource> MediumWaitingQueue {get;}
    private Queue<TaskCompletionSource> LowWaitingQueue {get;}

    private object Lock {get;}

    // The WaitingCount of the Snapshot sums the Count of the three Queues:
    public SchedulerSnapshot Snapshot
    {
        get
        {
            lock (this.Lock)
            {
                return new SchedulerSnapshot(
                    this.HasActiveRequest, 
                    this.HighWaitingQueue.Count + this.MediumWaitingQueue.Count + 
                    this.LowWaitingQueue.Count);
            }
        }
    }
    
    public PriorityScheduler()
        {
                this.HasActiveRequest = false;
                this.HighWaitingQueue = new Queue<TaskCompletionSource>();
                this.MediumWaitingQueue = new Queue<TaskCompletionSource>();
                this.LowWaitingQueue = new Queue<TaskCompletionSource>();
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

            // Otherwise, wait asynchronously based on Priority (then FIFO):

            TaskCompletionSource tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            if (requestInfo.Priority == PriorityLevel.High)
            {
                this.HighWaitingQueue.Enqueue(tcs);
            }
            else if (requestInfo.Priority == PriorityLevel.Medium)
            {
                this.MediumWaitingQueue.Enqueue(tcs);
            }
            else
            {
                this.LowWaitingQueue.Enqueue(tcs);
            }
            return tcs.Task;
        }
    }

    // Release() handles what happens when a request has been fully processed:
    public void Release()
    {
        TaskCompletionSource? next = null;

        lock (this.Lock)
        {
            // If there are other requests waiting, we set the Task object
            // corresponding to the first such request to completed.
            // That is, we admit that request.
            // We keep this.HasActiveRequest.
            // If there are no other requests waiting, we set this.HasActiveRequest
            // to false.

            // Note: if there are requests waiting, we only set the corresponding
            // Task object to completed once we have exited the lock block. This
            // is so that any resumed continuation does not run whilst the lock object
            // is still being held (whilst inside the "critical section").

            if (this.HighWaitingQueue.Count > 0)
            {
                next = this.HighWaitingQueue.Dequeue();
            }
            else if (this.MediumWaitingQueue.Count > 0)
            {
                next = this.MediumWaitingQueue.Dequeue();
            }
            else if (this.LowWaitingQueue.Count > 0)
            {
                next = this.LowWaitingQueue.Dequeue();
            }
            else
            {
                this.HasActiveRequest = false;
            }
        }

        if (next != null)
        {
            next.SetResult();
        }
    }
}