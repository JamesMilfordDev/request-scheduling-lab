# Request Scheduling Lab

An ASP.NET Core application exploring HTTP request handling as a scheduling problem,
implementing and comparing multiple scheduling algorithms within a concurrent system.

## Purpose

This project treats incoming HTTP requests as jobs competing for access to a limited resource: admission into processing.

Rather than allowing every request to proceed immediately, the application introduces an explicit scheduler into the middleware pipeline. Each request progresses through the following stages:

1. Arrives
2. Waits for admission according to a scheduling policy
3. Executes simulated work for a requested duration
4. Completes

The focus of the lab is to study how different scheduling algorithms affect request ordering, waiting time, and total response time under concurrent load.

The system is also designed to produce structured output that makes scheduler behaviour easy to inspect both at the event level and at the request summary level.

## Request Model

Each request is represented using metadata that allows the scheduler to make admission decisions.

Tracked request data:

- Request ID
- Path
- Requested duration (milliseconds, stored as an `int`)
- Priority (`Low`, `Medium`, `High`)

Scheduling algorithms may use these values to determine the order in which waiting requests are admitted.

The requested duration and priority are controlled through the request query string. For example:

`/work?duration=1000&priority=High`

Default values:

- Duration: `5000`
- Priority: `Medium`

## Inputs

The application expects one required command-line argument and one optional argument:

- `args[0]`: scheduler type
- `args[1]`: output filename prefix (optional)

If no output filename prefix is provided, the application uses the scheduler type as the base name.

## Outputs

The application writes two CSV files:

- `<base>_events.csv`
- `<base>_requests.csv`

Sample output files can be found in the `SampleOutput/` folder.

### Event Log

The first CSV is the event log. The following events are recorded:

- A request arrival
- A request admission
- A request completion

Each event log entry includes exactly:

- The time of the event (UTC)
- The ID of the request involved in the event
- The path of the request
- The requested duration of the request
- The priority of the request
- The event type
- Whether a request is being processed at the time of the event
- The number of requests waiting to be admitted at the time of the event

Each time an event is logged, the application generates a snapshot of the scheduler state at a single point in time, ensuring that the last two values are internally consistent.

The file is intended to show the exact sequence of events observed by the server. 

### Request Summaries Record

The second CSV is the request summaries record. The CSV contains exactly one request summary per completed request. Each request summary includes exactly:

Request metadata:

- ID
- Path
- Requested duration
- Priority

Timestamps (UTC):

- Arrival time
- Admission time
- Completion time

Derived metrics (milliseconds, stored as `int`):

- Waiting time (arrival to admission)
- Service time (admission to completion)
- Total time (arrival to completion)

## System Architecture

The system is divided into four components:

- Request model
- Scheduling
- Middleware pipeline
- Logging and reporting

The system is highly modular: the request model, middleware pipeline, and logging/reporting layer are all scheduler-agnostic.

### Request Model

This defines the immutable metadata associated with each request.

### Scheduling

This defines the scheduler abstraction (IScheduler) and the concrete scheduler class implementations.

Schedulers expose their current state via an immutable snapshot captured under the scheduler lock.

### Middleware Pipeline

A pipeline that coordinates request arrival, admission, execution, and completion.

### Logging and Reporting

This consists of:

- Logging: the real-time logging system that records all request events.

- Reporting: the generation of request summaries (metadata, timestamps, and derived metrics), and the writing of the event log and request summaries record.

## Scheduling Algorithm Evaluation Framework

At a global level (at which we don't privilege one kind of request over another), there are two primary evaluation metrics for scheduling algorithms:

- Efficiency
- Fairness

Efficiency is measured as the average request waiting time (arrival to admission). Fairness measurements can either focus on waiting time, or on the number of requests processed whilst a request is waiting (call this the SkippedOverCount of a request). A simple measurement is the maximum waiting time.

At the global level, two basic scheduling algorithms mark the extremes:

- FIFO: maximises fairness at the expense of efficiency.
- SJF: maximises efficiency at the expense of fairness.

Scheduling algorithms beyond these two fall into one of two categories:

- Global tradeoff algorithms: these iterate on SJF/FIFO to balance global efficiency and global fairness.

- Privileged algorithms: these explicitly prioritise certain kinds of request, and therefore do not optimise purely global metrics.

The schedulers implemented in this project illustrate these different approaches.

## Scheduling Algorithms Implemented

### FIFO

Requests are admitted in a simple FIFO order.

### Shortest Job First

Requests are admitted according to their requested duration (shortest duration first). Requests with equal durations are admitted in FIFO order.

### Priority

Requests are admitted according to their
priority level (High > Medium > Low). Requests within the same priority level are admitted in FIFO order.

This simple privileged algorithm optimises for fairness within each priority class. It risks both poor global efficiency and poor global fairness.

### Priority with SJF

Requests are admitted according to their priority level (High > Medium > Low). Requests within the same priority level are admitted according to their requested duration (shortest duration first). Requests with equal priority and requested durations are admitted in FIFO order.

This privileged algorithm complements the Priority algorithm. It optimises for efficiency within each priority class. It risks both poor global efficiency and poor global fairness.

### Bounded SJF

We have our two extreme global scheduling policies: FIFO and SJF.

We also have two complementary privileged algorithms: Priority and Priority with SJF.

Finally, a global tradeoff algorithm is implemented: Bounded SJF.

Requests are selected based on requested duration, with a fairness guarantee built in:

If a request has a `SkippedOverCount` greater than or equal to the `MaxSkippedOverCount`, choose the request with the greatest `SkippedOverCount`. If there are ties in `SkippedOverCount`, select according to FIFO.

If no request has a `SkippedOverCount` greater than or equal to the `MaxSkippedOverCount`, choose the request with the lowest requested duration. If there are ties in requested duration, select according to FIFO.

This global tradeoff algorithm strikes a balance between global efficiency and global fairness: we still process requests in a globally efficient way, but we also implement a boundary to prevent extreme global unfairness.

## Suggested Reading Order

The schedulers can be approached in two ways. Both perspectives are useful: one for understanding the conceptual framework, and one for understanding the evolution of the codebase.

### By Conceptual Framework

Start with the extreme global policies FIFO and SJF.

Then there are two branches.

For two complementary privileged algorithms, see Priority and Priority with SJF.

For a global tradeoff algorithm, see Bounded SJF.

### By Implementation Complexity

FIFO -> Priority -> SJF -> Priority with SJF -> Bounded SJF.

This follows the evolution of the codebase, introducing increasingly complex scheduling structures.

For example, the FIFO and Priority schedulers both represent a waiting request with a simple TaskCompletionSource instance. SJF and Priority with SJF both use the immutable `Waiter` class that stores a TCS alongside the `RequestedDuration` and a `SequenceNumber` for the request. Bounded SJF uses the richer `BoundedSjfWaiter`, which is checked and mutated as part of the scheduling process.

## Related Work

This project extends ideas first explored in the lift-simulation project, applying event-driven modelling and structured logging to a concurrent web environment.