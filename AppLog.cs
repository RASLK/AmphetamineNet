using System.Collections.Concurrent;
using System.Text;

namespace AmphetamineNet;

/// <summary>Buffered log — doesn't block UI/heartbeat on every AppendAllText.</summary>
internal static class AppLog
{
    private const string Path = "/tmp/amphetamine-net.log";
    private static readonly ConcurrentQueue<string> Queue = new();
    private static int _flushScheduled;

    public static void Write(string message)
    {
        Queue.Enqueue($"{DateTime.Now:O} {message}");
        ScheduleFlush();
    }

    private static void ScheduleFlush()
    {
        if (Interlocked.CompareExchange(ref _flushScheduled, 1, 0) != 0)
            return;

        ThreadPool.QueueUserWorkItem(static _ => Flush());
    }

    private static void Flush()
    {
        try
        {
            var sb = new StringBuilder();
            while (Queue.TryDequeue(out var line))
                sb.AppendLine(line);

            if (sb.Length > 0)
                File.AppendAllText(Path, sb.ToString());
        }
        catch
        {
            // ignore
        }
        finally
        {
            Interlocked.Exchange(ref _flushScheduled, 0);
            if (!Queue.IsEmpty)
                ScheduleFlush();
        }
    }
}
