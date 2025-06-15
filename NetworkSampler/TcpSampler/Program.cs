using System.Diagnostics;
using System.Net.Sockets;
using System.Text.Json;
using TcpSampler;

var json = await File.ReadAllTextAsync("appsettings.json");
var config = JsonSerializer.Deserialize<AppSettings>(json);

if (config == null)
{
    Console.WriteLine("Failed to deserialize app settings");
    return;
}

Console.WriteLine("Press Ctrl+C to exit or wait for the next TCP check...");

var fileWriter = new FileWriter(config.LogFilename);

while (true)
{
    var logTime = $"[{DateTime.Now.ToShortTimeString()}]";
    try
    {
        using TcpClient client = new();
        var stopwatch = Stopwatch.StartNew();
        client.Connect(config.Host, config.Port);
        stopwatch.Stop();

        if (client.Connected)
        {
            if (stopwatch.Elapsed.TotalMilliseconds > config.MaxResponseTime.TotalMilliseconds)
            {
                fileWriter.AppendToFile(config.Host, "Warning", stopwatch.Elapsed.TotalMilliseconds, "Response time exceeded maximum limit");
                Console.WriteLine($"{logTime} Warning: Response time {stopwatch.Elapsed.TotalMilliseconds}ms exceeds maximum {config.MaxResponseTime}ms");
            }
            else
            {
                fileWriter.AppendToFile(config.Host, "Success", stopwatch.Elapsed.TotalMilliseconds, null);
                Console.WriteLine($"{logTime} Reply from {config.Host}: time={stopwatch.Elapsed.TotalMilliseconds}ms");
            }
        }
        else
        {
            fileWriter.AppendToFile(config.Host, "Failed", 0, null);
            Console.WriteLine($"{logTime} TCP failed");
        }
    }
    catch (Exception ex)
    {
        fileWriter.AppendToFile(config.Host, "Fatal", 0, ex.Message);
        Console.WriteLine($"{logTime} TCP failed: {ex.Message}");
    }

    await Task.Delay(config.SampleInterval);
}