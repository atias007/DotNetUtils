using PingSampler;
using System.Net.NetworkInformation;
using System.Text.Json;

var json = await File.ReadAllTextAsync("appsettings.json");
var config = JsonSerializer.Deserialize<AppSettings>(json);

if (config == null)
{
    Console.WriteLine("Failed to deserialize app settings");
    return;
}

Console.WriteLine("Press Ctrl+C to exit or wait for the next ping...");

using Ping ping = new();
var fileWriter = new FileWriter(config.LogFilename);
while (true)
{
    var logTime = $"[{DateTime.Now.ToShortTimeString()}]";
    try
    {
        var reply = await ping.SendPingAsync(config.Host, config.Port);
        if (reply.Status == IPStatus.Success)
        {
            if (reply.RoundtripTime > config.MaxResponseTime.TotalMilliseconds)
            {
                fileWriter.AppendToFile(reply.Address.ToString(), "Warning", reply.RoundtripTime, "Response time exceeded maximum limit");
                Console.WriteLine($"{logTime} Warning: Response time {reply.RoundtripTime}ms exceeds maximum {config.MaxResponseTime}ms");
            }
            else
            {
                fileWriter.AppendToFile(reply.Address.ToString(), "Success", reply.RoundtripTime, null);
                Console.WriteLine($"{logTime} Reply from {reply.Address}: time={reply.RoundtripTime}ms");
            }
        }
        else
        {
            fileWriter.AppendToFile(reply.Address.ToString(), "Failed", 0, reply.Status.ToString());
            Console.WriteLine($"{logTime} Ping failed: {reply.Status}");
        }
    }
    catch (Exception ex)
    {
        fileWriter.AppendToFile(config.Host, "Fatal", 0, ex.Message);
        Console.WriteLine($"{logTime} Ping failed: {ex.Message}");
    }

    await Task.Delay(config.SampleInterval);
}