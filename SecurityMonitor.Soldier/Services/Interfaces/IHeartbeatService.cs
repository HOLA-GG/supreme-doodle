using SecurityMonitorAgent.Models;

namespace SecurityMonitorAgent.Services.Interfaces;

public interface IHeartbeatService
{
    Task<bool> SendHeartbeatAsync(HeartbeatPayload payload);
}
