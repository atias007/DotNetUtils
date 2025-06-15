namespace TcpSampler;

public class AppSettings
{
    public required string Host { get; set; }
    public required string LogFilename { get; set; }
    public required int Port { get; set; }
    public required TimeSpan MaxResponseTime { get; set; }
    public required TimeSpan SampleInterval { get; set; }
}