using Microsoft.Extensions.DependencyInjection;

namespace RequestSchedulingLab;

public class Program
{
    public static void Main(string[] args)
    {
        // Initial input check:

        if (args.Length == 0 || args.Length > 2)
        {
            throw new ArgumentException("Expected 1 or 2 arguments: a scheduler type and an optional output filename prefix.");
        }

        // WebApplicationBuilder object created:

        var builder = WebApplication.CreateBuilder(args);

        // Register the scheduler class determined by args[0].
        // We use AddSingleton so that the same scheduler class instance 
        // is used across all request execution flows:

        string schedulerType = args[0].ToLowerInvariant();
        switch (schedulerType)
        {
            case "fifo":
                builder.Services.AddSingleton<IScheduler, FifoScheduler>();
                break;
            default:
                throw new ArgumentException("Invalid scheduler type: expected one of: fifo.");

        }

        // Register the log class. We again use AddSingleton so that the same
        // Log instance is used across all request execution flows:

        builder.Services.AddSingleton<Log>();

        // WebApplication object created:

        var app = builder.Build();

        // Register that the event log and request summary CSV files should be created
        // when the application stops running.

        string outputBaseName;
        if (args.Length == 1)
        {
            outputBaseName = schedulerType;
        }
        else
        {
            outputBaseName = args[1];
        }

        Log log = app.Services.GetRequiredService<Log>();
        app.Lifetime.ApplicationStopping.Register(() =>
        {
            log.WriteToCsv(outputBaseName + "_events.csv");
            log.WriteRequestSummariesToCsv(outputBaseName + "_requests.csv");
        });


        // Middleware (completely scheduler agnostic):

        app.Use(async (context, next) =>
        {
            // Access the singleton scheduler and log:

            IScheduler scheduler = context.RequestServices.GetRequiredService<IScheduler>();
            Log log = context.RequestServices.GetRequiredService<Log>();

            // Gather the relevant metadata for the request:

            string requestId = Guid.NewGuid().ToString();

            string path = context.Request.Path.ToString();
            
            // for requestedDuration: set default value, then check
            // if the query string sets its own value. If it does, set it 
            // to that (with certain min and max limits):

            int requestedDuration = 5000;
            if (context.Request.Query.TryGetValue("duration", out var durationValue)
                && int.TryParse(durationValue, out int parsedDuration))
                {
                    requestedDuration = parsedDuration;
                }
            requestedDuration = Math.Clamp(requestedDuration, 1, 10000);

            // for priority: set default to Medium, then check if the query
            // string sets its own value. Override if it does appropriately.
            // The "true" argument tells TryParse() to ignore case differences.

            PriorityLevel priority = PriorityLevel.Medium;
            if (context.Request.Query.TryGetValue("priority", out var priorityValue)
                && Enum.TryParse<PriorityLevel>(priorityValue, ignoreCase: true, out PriorityLevel parsedPriority))
                {
                    priority = parsedPriority;
                }


            // Generate a RequestInfo instance for the request:

            RequestInfo requestInfo = new RequestInfo(requestId, path, requestedDuration, priority);

            
            // Associate the RequestInfo instance with the request via its HttpContext:

            context.Items["RequestInfo"] = requestInfo;



            // Log the request arrival.
            // This has two parts:
            // (A) Generate the relevant LogEntry object.
            // (B) Call AddEntry() on log, passing in the new LogEntry object.

            SchedulerSnapshot snapshot = scheduler.Snapshot;

            log.AddEntry(new LogEntry(
                DateTime.UtcNow,
                requestInfo.RequestId,
                requestInfo.Path,
                requestInfo.RequestedDuration,
                requestInfo.Priority,
                EventType.Arrived,
                snapshot.HasActiveRequest,
                snapshot.WaitingCount    
            ));


            // Our scheduler processes the request arrival:
            
            await scheduler.WaitForTurnAsync(requestInfo);

            // Once the request has been admitted, log the request admission:

            snapshot = scheduler.Snapshot;

            log.AddEntry(new LogEntry(
                DateTime.UtcNow,
                requestInfo.RequestId,
                requestInfo.Path,
                requestInfo.RequestedDuration,
                requestInfo.Priority,
                EventType.Admitted,
                snapshot.HasActiveRequest,
                snapshot.WaitingCount    
            ));

            // Continue through the middleware pipeline.

            try 
            {
                await next();
            }
            finally
            {
                // Log the request completion:

                snapshot = scheduler.Snapshot;

                log.AddEntry(new LogEntry(
                    DateTime.UtcNow,
                    requestInfo.RequestId,
                    requestInfo.Path,
                    requestInfo.RequestedDuration,
                    requestInfo.Priority,
                    EventType.Completed,
                    snapshot.HasActiveRequest,
                    snapshot.WaitingCount    
                ));

                // Release the scheduler:

                scheduler.Release();
            }

        });

        // Endpoint mapping:

        app.MapGet("/work", async (HttpContext context) =>
        {
            RequestInfo requestInfo = (RequestInfo)context.Items["RequestInfo"]!;
            await Task.Delay(requestInfo.RequestedDuration);
            return Results.Ok("Done");
        });


        app.Run();
    }
}
