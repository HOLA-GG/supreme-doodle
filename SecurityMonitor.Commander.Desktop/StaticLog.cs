using System;

namespace SecurityMonitor.Commander.Desktop
{
    public static class StaticLog
    {
        public static event Action<SecurityMonitor.Shared.Models.HeartbeatDto>? OnHeartbeat;
        public static event Action<string>? OnSystemMessage;

        public static void LogHeartbeat(SecurityMonitor.Shared.Models.HeartbeatDto heartbeat)
        {
            OnHeartbeat?.Invoke(heartbeat);
        }

        public static void LogMessage(string message)
        {
            OnSystemMessage?.Invoke(message);
        }
    }
}
