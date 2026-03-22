// A thread-safe in-memory log of request lifecycle events,
// stored as LogEntry instances, with CSV export support.

using System.Globalization;
using CsvHelper;

namespace RequestSchedulingLab;
public class Log
{
    private List<LogEntry> Entries {get;}

    // In order to handle the situation in which multiple
    // requests want to interact with Entries near
    // simultaneously, we use a lock so that only one execution flow at a time 
    // can inspect and update the shared log.
    // LogLock is used as the lock object:
    private object Lock {get;}

    public Log()
    {
        this.Entries = new List<LogEntry>();
        this.Lock = new object();
    }

    public void AddEntry(LogEntry entry)
    {
        lock (this.Lock)
        {
            this.Entries.Add(entry);
        }
    }

    public List<LogEntry> GetEntries()
    {
        lock(this.Lock)
        {
            return new List<LogEntry>(this.Entries);
        }
    }

    public List<RequestSummary> GetRequestSummaries()
    {
        List<RequestSummary> returnList = new List<RequestSummary>();
        List<LogEntry> logEntryList = this.GetEntries();

        // Create an IEnumerable<IGrouping<string, LogEntry>> that groups the
        // events in logEntryList by the RequestId string. This gives us one group
        // per request:

        var groups = logEntryList.GroupBy(e => e.RequestId);

        // Iterate through the groups (the IGrouping<string, LogEntry> instances).
        // For each group, define the corresponding RequestSummary object and add
        // it to returnList.

        foreach (var group in groups)
        {
            string requestId = group.Key;

            // IGrouping<string, LogEntry> implements IEnumerable<LogEntry>.
            // This will have three elements, distinguished by the EventType:

            LogEntry arrivedLogEntry = group.First(e => e.Event == EventType.Arrived);
            LogEntry admittedLogEntry = group.First(e => e.Event == EventType.Admitted);
            LogEntry completedLogEntry = group.First(e => e.Event == EventType.Completed);

            // Shared request metadata can be read from any of the LogEntry objects
            // in the group. We use arrivedLogEntry:

            string path = arrivedLogEntry.Path;
            int requestedDuration = arrivedLogEntry.RequestedDuration;
            PriorityLevel priority = arrivedLogEntry.Priority;

            // Extract timestamps from the three LogEntry objects in the group:

            DateTime arrivedAt = arrivedLogEntry.Timestamp;
            DateTime admittedAt = admittedLogEntry.Timestamp;
            DateTime completedAt = completedLogEntry.Timestamp;

            // Build the corresponding RequestSummary object with this information:

            RequestSummary requestSummary = new RequestSummary(
                requestId,
                path,
                requestedDuration,
                priority,
                arrivedAt,
                admittedAt,
                completedAt
            );

            // Add the RequestSummary object to returnList:

            returnList.Add(requestSummary);
        }

        return returnList;
    }

    public void WriteToCsv(string filePath)
    {
        try
        {
            using (StreamWriter sw = new StreamWriter(filePath))
            using (CsvWriter csv = new CsvWriter(sw, CultureInfo.InvariantCulture))
            {
                csv.WriteRecords(this.GetEntries());
            }
            Console.WriteLine($"Log written to {filePath}");
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to write log: {e.Message}");
        }
    }

    public void WriteRequestSummariesToCsv(string filePath)
    {
        try
        {
            using (StreamWriter sw = new StreamWriter(filePath))
            using (CsvWriter csv = new CsvWriter(sw, CultureInfo.InvariantCulture))
            {
                csv.WriteRecords(this.GetRequestSummaries());
            }
            Console.WriteLine($"Request summaries written to {filePath}");
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to write request summaries: {e.Message}");
        }
    }

}