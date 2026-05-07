using SecurityMonitorAgent.Models;

namespace SecurityMonitorAgent.Services.Interfaces;

public interface ILocalStorageService
{
    void SaveAlert(HeartbeatPayload payload);
    IEnumerable<AlertRecord> GetPendingAlerts();
    void MarkAsSent(int alertId);
}
