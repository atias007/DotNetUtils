using System.Collections.Concurrent;
using Timer = System.Timers.Timer;

namespace PingSampler;

internal class FileWriter
{
    private readonly ConcurrentQueue<string> logQueue = new();
    private readonly Timer writeTimer = new(TimeSpan.FromMinutes(1));
    private readonly SemaphoreSlim semaphore = new(1, 1);
    private readonly string filename;

    public FileWriter(string filename)
    {
        writeTimer.Elapsed += async (s, e) => await WriteTimerElapsed();
        writeTimer.Start();
        this.filename = filename;
    }

    private async Task WriteTimerElapsed()
    {
        await semaphore.WaitAsync();
        string? logEntry = string.Empty;

        try
        {
            writeTimer.Stop(); // Stop the timer to prevent concurrent writes
            while (logQueue.TryDequeue(out logEntry))
            {
                await File.AppendAllTextAsync(filename, logEntry + Environment.NewLine);
            }
        }
        catch (Exception ex)
        {
            logQueue.Enqueue(logEntry ?? string.Empty); // Re-enqueue the last entry if an error occurs
            Console.WriteLine($"Error writing to file: {ex.Message}");
        }
        finally
        {
            semaphore.Release();
            writeTimer.Start(); // Restart the timer for the next write operation
        }
    }

    public void AppendToFile(string address, string status, double roundtripTime, string? info)
    {
        var logEntry = $"{DateTime.Now.ToShortTimeString()},{address},{status},{roundtripTime},{info}";
        logQueue.Enqueue(logEntry);
    }
}