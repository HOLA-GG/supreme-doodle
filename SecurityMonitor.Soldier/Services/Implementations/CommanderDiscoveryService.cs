using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SecurityMonitorAgent.Services.Implementations;

/// <summary>
/// Escucha el broadcast UDP del Comandante y actualiza automáticamente
/// la URL de destino para los heartbeats. Sin necesidad de configuración manual de IP.
/// Puerto UDP: 47777
/// </summary>
public class CommanderDiscoveryService : BackgroundService
{
    private readonly ILogger<CommanderDiscoveryService> _logger;
    private const int BroadcastPort = 47777;
    private const string MagicHeader = "SECURITY_MONITOR_COMMANDER:";

    // URL compartida entre esta clase y HeartbeatService
    public static string? DiscoveredCommanderUrl { get; private set; }

    public CommanderDiscoveryService(ILogger<CommanderDiscoveryService> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Discovery iniciado. Escuchando broadcasts del Comandante en UDP:{Port}", BroadcastPort);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var udpClient = new UdpClient(BroadcastPort);
                udpClient.Client.ReceiveTimeout = 15000; // 15 segundos de espera

                var result = await udpClient.ReceiveAsync(stoppingToken);
                var message = Encoding.UTF8.GetString(result.Buffer);

                if (message.StartsWith(MagicHeader))
                {
                    var newUrl = message.Substring(MagicHeader.Length).Trim();
                    if (newUrl != DiscoveredCommanderUrl)
                    {
                        DiscoveredCommanderUrl = newUrl;
                        _logger.LogInformation("¡Comandante descubierto automáticamente en: {Url}", newUrl);
                    }
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogDebug("Esperando broadcast del Comandante... ({Error})", ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
