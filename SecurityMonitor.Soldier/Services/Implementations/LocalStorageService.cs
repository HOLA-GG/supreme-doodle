using LiteDB;
using SecurityMonitorAgent.Models;
using SecurityMonitorAgent.Services.Interfaces;

namespace SecurityMonitorAgent.Services.Implementations;

public class LocalStorageService : ILocalStorageService
{
    private readonly string _dbPath;

    public LocalStorageService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var dir = Path.Combine(appData, "SecurityMonitorAgent");
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        _dbPath = Path.Combine(dir, "agent_alerts.db");
    }

    public void SaveAlert(HeartbeatPayload payload)
    {
        using var db = new LiteDatabase(_dbPath);
        var col = db.GetCollection<AlertRecord>("alerts");
        col.Insert(new AlertRecord
        {
            Payload = payload,
            CreatedAt = DateTime.UtcNow,
            Sent = false
        });
    }

    public IEnumerable<AlertRecord> GetPendingAlerts()
    {
        using var db = new LiteDatabase(_dbPath);
        var col = db.GetCollection<AlertRecord>("alerts");
        return col.Find(x => !x.Sent).ToList();
    }

    public void MarkAsSent(int alertId)
    {
        using var db = new LiteDatabase(_dbPath);
        var col = db.GetCollection<AlertRecord>("alerts");
        var alert = col.FindById(alertId);
        if (alert != null)
        {
            alert.Sent = true;
            col.Update(alert);
        }
    }
}
