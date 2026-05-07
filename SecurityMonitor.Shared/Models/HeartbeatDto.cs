using System;

namespace SecurityMonitor.Shared.Models
{
    public class HeartbeatDto
    {
        public string MachineName { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string MacAddress { get; set; } = string.Empty; // ID de fábrica (no cambia con reset)
        public string HardwareFingerprint { get; set; } = string.Empty; // MAC + BIOS + GUID combinado
        public string IpAddress { get; set; } = string.Empty;
        public string ConnectionType { get; set; } = string.Empty; // "WiFi", "Ethernet", etc.
        public string CurrentNetworkSsid { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public bool IsInAllowedRadius { get; set; }
        public bool IsOnAuthorizedNetwork { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
