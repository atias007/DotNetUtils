namespace DemoAzureFunc.Models;

internal class QueueDetails
{
    public string QueueName { get; set; } = string.Empty;
    public int Total { get; set; }
    public int Unack { get; set; }
    public int Consumers { get; set; }
    public DateTimeOffset UpdateDate { get; set; } = DateTime.UtcNow;
}