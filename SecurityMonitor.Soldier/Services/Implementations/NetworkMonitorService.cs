using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using SecurityMonitorAgent.Services.Interfaces;

namespace SecurityMonitorAgent.Services.Implementations;

public class NetworkMonitorService : INetworkMonitorService
{
    public event EventHandler? NetworkChanged;

    public NetworkMonitorService()
    {
        NetworkChange.NetworkAddressChanged += (sender, args) => NetworkChanged?.Invoke(this, EventArgs.Empty);
    }

    public string GetCurrentSsid()
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = "wlan show interfaces",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };
            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            var line = output.Split(new[] { "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                             .FirstOrDefault(l => l.TrimStart().StartsWith("SSID") && !l.Contains("BSSID"));

            if (line != null)
            {
                var idx = line.IndexOf(':');
                if (idx >= 0) return line.Substring(idx + 1).Trim();
            }
        }
        catch { /* Fallback a otros métodos */ }
        return string.Empty;
    }

    public bool IsInAuthorizedNetwork(string authorizedSsidOrIp)
    {
        if (string.IsNullOrWhiteSpace(authorizedSsidOrIp)) return true; // Si no hay red configurada, siempre true

        // 1. Comparar SSID real
        string currentSsid = GetCurrentSsid();
        if (!string.IsNullOrEmpty(currentSsid) &&
            currentSsid.Equals(authorizedSsidOrIp, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // 2. Verificar por prefijo de IP (fallback para Ethernet)
        var localIp = GetLocalIpAddress();
        if (!string.IsNullOrEmpty(localIp) && localIp.StartsWith(authorizedSsidOrIp))
            return true;

        // 3. Verificar por nombre de adaptador
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus == OperationalStatus.Up)
            {
                if (ni.Name.Contains(authorizedSsidOrIp, StringComparison.OrdinalIgnoreCase) ||
                    ni.Description.Contains(authorizedSsidOrIp, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }

    public string GetMacAddress()
    {
        var active = NetworkInterface.GetAllNetworkInterfaces()
            .FirstOrDefault(ni => ni.OperationalStatus == OperationalStatus.Up &&
                                  ni.NetworkInterfaceType != NetworkInterfaceType.Loopback);
        return active?.GetPhysicalAddress().ToString() ?? "UNKNOWN_MAC";
    }

    public string GetLocalIpAddress()
    {
        try
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up && 
                             ni.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                             !ni.Description.Contains("Virtual", StringComparison.OrdinalIgnoreCase));

            foreach (var ni in interfaces)
            {
                var props = ni.GetIPProperties();
                var ipv4 = props.UnicastAddresses.FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork);
                if (ipv4 != null) return ipv4.Address.ToString();
            }
        }
        catch { }

        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
        try
        {
            socket.Connect("8.8.8.8", 65530);
            if (socket.LocalEndPoint is IPEndPoint endPoint)
                return endPoint.Address.ToString();
        }
        catch { }
        return "127.0.0.1";
    }

    public string GetActiveConnectionType()
    {
        try
        {
            // Primero determinar la IP activa que tiene salida a internet
            string activeIp = "127.0.0.1";
            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
            {
                socket.Connect("8.8.8.8", 65530);
                if (socket.LocalEndPoint is IPEndPoint endPoint)
                    activeIp = endPoint.Address.ToString();
            }

            var active = NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up &&
                             ni.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                             ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                .FirstOrDefault(ni => ni.GetIPProperties().UnicastAddresses.Any(ip => ip.Address.ToString() == activeIp));

            if (active == null)
            {
                // Fallback si no encuentra por IP
                active = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(ni => ni.OperationalStatus == OperationalStatus.Up &&
                                 ni.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                                 ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                    .OrderByDescending(ni => ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet ? 1 : 0)
                    .FirstOrDefault(ni => ni.GetIPProperties().UnicastAddresses.Any(ip => ip.Address.AddressFamily == AddressFamily.InterNetwork));
            }

            if (active != null)
            {
                if (active.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                    return "WiFi";
                if (active.NetworkInterfaceType == NetworkInterfaceType.Ethernet || active.Description.Contains("Ethernet", StringComparison.OrdinalIgnoreCase))
                    return "Ethernet";
                
                return active.NetworkInterfaceType.ToString();
            }
        }
        catch { }
        return "Unknown";
    }
}
