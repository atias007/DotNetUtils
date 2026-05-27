using StackExchange.Redis;

namespace Publisher;

public class Program
{
    private const string StreamName = "messages-stream";
    private const string RedisConnectionString = "localhost:6379";

    public static async Task Main(string[] args)
    {
        Console.WriteLine("=== Redis Streams Publisher ===");
        Console.WriteLine();

        // Connect to Redis
        ConnectionMultiplexer redis;
        try
        {
            redis = await ConnectionMultiplexer.ConnectAsync(RedisConnectionString);
            Console.WriteLine($"Connected to Redis at {RedisConnectionString}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to connect to Redis: {ex.Message}");
            Console.WriteLine("Make sure Redis is running on localhost:6379");
            return;
        }

        var db = redis.GetDatabase();
        Console.WriteLine($"Publishing to stream: {StreamName}");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  Type a message and press Enter to publish");
        Console.WriteLine("  Type 'quit' or 'exit' to stop");
        Console.WriteLine();

        int messageCount = 0;

        while (true)
        {
            Console.Write("Message> ");
            var input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
                continue;

            if (input.Equals("quit", StringComparison.OrdinalIgnoreCase) ||
                input.Equals("exit", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Shutting down publisher...");
                break;
            }

            try
            {
                // Create message with metadata
                var entries = new NameValueEntry[]
                {
                    new("content", input),
                    new("sender", Environment.MachineName),
                    new("timestamp", DateTime.UtcNow.ToString("O")),
                    new("sequence", (++messageCount).ToString())
                };

                // Add to stream - Redis auto-generates the message ID
                var messageId = await db.StreamAddAsync(StreamName, entries);

                Console.WriteLine($"  Published with ID: {messageId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Error publishing: {ex.Message}");
            }
        }

        redis.Close();
        Console.WriteLine("Publisher stopped.");
    }
}