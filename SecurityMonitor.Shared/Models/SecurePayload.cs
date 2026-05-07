namespace SecurityMonitor.Shared.Models;

public class SecurePayload
{
    public string Data { get; set; } = string.Empty; // HeartbeatDto serializado y encriptado
    public string Signature { get; set; } = string.Empty; // HMAC-SHA256
    public string MachineName { get; set; } = string.Empty; 
    public long TimestampTicks { get; set; } // Para Anti-Replay
}
