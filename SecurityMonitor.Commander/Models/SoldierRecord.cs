using System;

namespace SecurityMonitor.Commander.Models
{
    public class SoldierRecord
    {
        public string Id { get; set; } = string.Empty; // MachineName or unique ID
        public string MachineName { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string MacAddress { get; set; } = string.Empty;
        public string HardwareFingerprint { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public string ConnectionType { get; set; } = string.Empty;
        public string CurrentNetworkSsid { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public bool IsInAllowedRadius { get; set; }
        public bool IsOnAuthorizedNetwork { get; set; }
        public DateTime LastSeen { get; set; }
    }
}
