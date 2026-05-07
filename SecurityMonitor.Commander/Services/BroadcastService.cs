using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SecurityMonitor.Commander.Services
{
    /// <summary>
    /// Transmite la presencia del Comandante en la red mediante UDP Broadcast.
    /// El Soldado escucha este broadcast y se auto-configura para reportarse.
    /// Puerto UDP: 47777
    /// Mensaje: "SECURITY_MONITOR_COMMANDER:http://[IP]:5000"
    /// </summary>
    public class BroadcastService : BackgroundService
    {
        private readonly ILogger<BroadcastService> _logger;
        private const int BroadcastPort = 47777;
        private const string MagicHeader = "SECURITY_MONITOR_COMMANDER:";

        public BroadcastService(ILogger<BroadcastService> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("BroadcastService iniciado. Transmitiendo presencia en UDP:{Port}", BroadcastPort);

            using var udpClient = new UdpClient();
            udpClient.EnableBroadcast = true;

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var myIp = GetLocalIpAddress();
                    var message = $"{MagicHeader}http://{myIp}:5000";
                    var data = Encoding.UTF8.GetBytes(message);

                    // Broadcast a toda la subred
                    var endpoint = new IPEndPoint(IPAddress.Broadcast, BroadcastPort);
                    await udpClient.SendAsync(data, data.Length, endpoint);

                    _logger.LogDebug("Broadcast enviado: {Message}", message);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error enviando broadcast UDP.");
                }

                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }

        private static string GetLocalIpAddress()
        {
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
    }
}
