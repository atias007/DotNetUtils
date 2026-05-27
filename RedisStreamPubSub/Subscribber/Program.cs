using StackExchange.Redis;

namespace Subscriber;

public class Program
{
    private const string StreamName = "messages-stream";
    private const string RedisConnectionString = "localhost:6379";

    public static async Task Main(string[] args)
    {
        Console.WriteLine("=== Redis Streams Subscriber (Broadcast Mode) ===");
        Console.WriteLine();

        // Each subscriber gets its own unique consumer group = broadcast semantics
        // Every instance receives ALL messages
        var instanceId = Guid.NewGuid().ToString("N")[..8];
        var consumerGroupName = $"subscriber-{instanceId}";
        var consumerName = $"consumer-{instanceId}";

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

        // Create consumer group for this instance
        await EnsureConsumerGroupExistsAsync(db, consumerGroupName);

        Console.WriteLine($"Stream: {StreamName}");
        Console.WriteLine($"Consumer Group: {consumerGroupName} (unique per instance)");
        Console.WriteLine($"Consumer Name: {consumerName}");
        Console.WriteLine();
        Console.WriteLine("Listening for messages... (Press Ctrl+C to stop)");
        Console.WriteLine(new string('-', 60));

        // Setup cancellation for graceful shutdown
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
            Console.WriteLine("\nShutdown requested...");
        };

        // First, process any pending messages (messages that were delivered but not acknowledged)
        await ProcessPendingMessagesAsync(db, consumerGroupName, consumerName, cts.Token);

        // Then, listen for new messages
        await ListenForMessagesAsync(db, consumerGroupName, consumerName, cts.Token);

        redis.Close();
        Console.WriteLine("Subscriber stopped.");
    }

    private static async Task EnsureConsumerGroupExistsAsync(IDatabase db, string consumerGroupName)
    {
        try
        {
            // Create consumer group starting from the end of the stream
            // Use "$" to only read NEW messages (not historical)
            // Each subscriber instance gets its own group = broadcast to all
            await db.StreamCreateConsumerGroupAsync(
                StreamName,
                consumerGroupName,
                "$",  // Start from NOW - only new messages
                createStream: true);

            Console.WriteLine($"Created consumer group: {consumerGroupName}");
        }
        catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP"))
        {
            // Consumer group already exists - this is fine
            Console.WriteLine($"Consumer group already exists: {consumerGroupName}");
        }
    }

    private static async Task ProcessPendingMessagesAsync(
        IDatabase db,
        string consumerGroupName,
        string consumerName,
        CancellationToken cancellationToken)
    {
        Console.WriteLine("Checking for pending messages...");

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Read pending messages for this consumer
                var pendingMessages = await db.StreamReadGroupAsync(
                    StreamName,
                    consumerGroupName,
                    consumerName,
                    position: "0",  // "0" reads pending messages
                    count: 10);

                if (pendingMessages.Length == 0)
                {
                    Console.WriteLine("No pending messages.");
                    break;
                }

                foreach (var entry in pendingMessages)
                {
                    await ProcessMessageAsync(db, consumerGroupName, entry, isPending: true);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing pending messages: {ex.Message}");
                break;
            }
        }
    }

    private static async Task ListenForMessagesAsync(
        IDatabase db,
        string consumerGroupName,
        string consumerName,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Read new messages with blocking
                // ">" means only new messages not yet delivered to any consumer
                var messages = await db.StreamReadGroupAsync(
                    StreamName,
                    consumerGroupName,
                    consumerName,
                    position: ">",  // Only new messages
                    count: 10);

                if (messages.Length == 0)
                {
                    // No messages available, wait a bit before polling again
                    await Task.Delay(500, cancellationToken);
                    continue;
                }

                foreach (var entry in messages)
                {
                    await ProcessMessageAsync(db, consumerGroupName, entry, isPending: false);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading messages: {ex.Message}");
                await Task.Delay(1000, cancellationToken);
            }
        }
    }

    private static async Task ProcessMessageAsync(IDatabase db, string consumerGroupName, StreamEntry entry, bool isPending)
    {
        var messageId = entry.Id;
        var values = entry.Values;

        // Extract message fields
        var content = values.FirstOrDefault(v => v.Name == "content").Value.ToString();
        var sender = values.FirstOrDefault(v => v.Name == "sender").Value.ToString();
        var timestamp = values.FirstOrDefault(v => v.Name == "timestamp").Value.ToString();
        var sequence = values.FirstOrDefault(v => v.Name == "sequence").Value.ToString();

        // Display the message
        var prefix = isPending ? "[PENDING] " : "";
        Console.WriteLine();
        Console.WriteLine($"{prefix}Message ID: {messageId}");
        Console.WriteLine($"  Content:   {content}");
        Console.WriteLine($"  Sender:    {sender}");
        Console.WriteLine($"  Timestamp: {timestamp}");
        Console.WriteLine($"  Sequence:  {sequence}");

        // Simulate some processing time
        await Task.Delay(50);

        // Acknowledge the message (mark as processed)
        await db.StreamAcknowledgeAsync(StreamName, consumerGroupName, messageId);
        Console.WriteLine($"  Status:    Acknowledged ✓");
    }
}