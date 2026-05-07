using System.Text.Json.Serialization;

namespace SecurityMonitorAgent.Models;

public class HeartbeatPayload
{
    [JsonPropertyName("accountId")]
    public string AccountId { get; set; } = string.Empty;

    [JsonPropertyName("hostname")]
    public string Hostname { get; set; } = string.Empty;

    [JsonPropertyName("macAddress")]
    public string MacAddress { get; set; } = string.Empty;

    [JsonPropertyName("localIp")]
    public string LocalIp { get; set; } = string.Empty;

    [JsonPropertyName("connectionType")]
    public string ConnectionType { get; set; } = string.Empty;

    [JsonPropertyName("currentSsid")]
    public string CurrentSsid { get; set; } = string.Empty;

    [JsonPropertyName("latitude")]
    public double Latitude { get; set; }

    [JsonPropertyName("longitude")]
    public double Longitude { get; set; }

    [JsonPropertyName("isAlarmActive")]
    public bool IsAlarmActive { get; set; }

    [JsonPropertyName("alarmReason")]
    public string? AlarmReason { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }
}
