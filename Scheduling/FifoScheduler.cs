// A FIFO request scheduler.

using System.Collections.Generic;
using System.Threading.Tasks;

namespace RequestSchedulingLab;
public class FifoScheduler : IScheduler
{
    // The scheduler allows one request at a time to be admitted.
    // We represent whether the scheduler has an active request with a bool:
    private bool HasActiveRequest {get; set;}

    // Each waiting request gets its own TaskCompletionSource.
    // The request awaits the corresponding Task.
    // When the request is ready to be admitted, we complete
    // the Task via the corresponding TaskCompletionSource.
    // We capture FIFO scheduling by using a Queue<TaskCompletionSource>:
    private Queue<TaskCompletionSource> WaitingQueue {get;}

    // In order to handle the situation in which multiple request execution
    // flows want to interact with the shared state {HasActiveRequest, WaitingQueue}
    // near simultaneously, we use a lock so that only one execution flow at a 
    // time can inspect and update the shared state:
    private object Lock {get;}

    // For reporting purposes, we need to access HasActiveRequest and
    // WaitingQueue.Count. We want to access both of these properties 
    // within the same lock handling. For this purpose, we generate a 
    // SchedulerSnapshot instance:

    public SchedulerSnapshot Snapshot
    {
        get
        {
            lock (this.Lock)
            {
                return new SchedulerSnapshot(
                    this.HasActiveRequest, 
                    this.WaitingQueue.Count);
            }
        }
    }
    
    public FifoScheduler()
        {
                this.HasActiveRequest = false;
                this.WaitingQueue = new Queue<TaskCompletionSource>();
                this.Lock = new object();
        }


    // WaitForTurnAsync() handles a request as it reaches the scheduler. It decides
    // whether the request may be admitted immediately or must wait asynchronously:
    public Task WaitForTurnAsync(RequestInfo requestInfo)
    {
        // As we are interacting with the shared state (this.HasActiveRequest and
        // this.WaitingQueue), we require the lock.

        lock (this.Lock)
        {
            // Immediate admission if no request is being processed.

            if (!this.HasActiveRequest)
            {
                this.HasActiveRequest = true;
                return Task.CompletedTask;   
            }

            // Otherwise, wait asynchronously in FIFO order:

            TaskCompletionSource tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            this.WaitingQueue.Enqueue(tcs);
            return tcs.Task;
        }
    }

    // Note: there are two kinds of waiting in the design:
    // (A) Waiting for the lock.
    // Synchronous, thread-blocking, short-lived, handled by runtime.
    // (B) Waiting for admission by the FIFO scheduler.
    // Asynchronous, non-blocking, potentially long-lived,
    // implemented via TaskCompletionSource.


    // Release() handles what happens when a request has been fully processed:
    public void Release()
    {
        TaskCompletionSource? next = null;

        // As we are interacting with the shared state (this.HasActiveRequest and
        // this.WaitingQueue), we require the lock.

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

            if (this.WaitingQueue.Count > 0)
            {
                next = this.WaitingQueue.Dequeue();
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