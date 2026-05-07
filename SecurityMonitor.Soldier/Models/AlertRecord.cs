namespace SecurityMonitorAgent.Models;

public class AlertRecord
{
    public int Id { get; set; }
    public HeartbeatPayload Payload { get; set; } = new();
    public bool Sent { get; set; }
    public DateTime CreatedAt { get; set; }
}
