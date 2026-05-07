using SecurityMonitorAgent.Services.Implementations;
using SecurityMonitorAgent.Services.Interfaces;
using SecurityMonitorAgent.Workers;
using NReco.Logging.File;

namespace SecurityMonitorAgent;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        // Correr como Windows Service
        builder.Services.AddWindowsService(options =>
        {
            options.ServiceName = "Security Monitor Soldier";
        });

        // Logging a archivo para diagnóstico fácil (ver en: {InstallDir}\Logs\soldier-YYYY-MM-DD.log)
        var logDir = Path.Combine(AppContext.BaseDirectory, "Logs");
        Directory.CreateDirectory(logDir);
        builder.Logging.AddFile(Path.Combine(logDir, "soldier-{0:yyyy-MM-dd}.log"), fileLoggerOpts =>
        {
            fileLoggerOpts.FormatLogEntry = (msg) =>
                $"[{msg.LogLevel}] {DateTime.Now:HH:mm:ss} {msg.LogName}: {msg.Message}{(msg.Exception != null ? "\n" + msg.Exception : "")}";
        });
        
        // Configurar HttpClient - BaseAddress se actualiza dinámicamente
        var heartbeatUrl = builder.Configuration.GetValue<string>("AgentSettings:HeartbeatEndpointUrl")
                           ?? "http://localhost:5000/api/heartbeat";
        var uri = new Uri(heartbeatUrl);
        var baseUrl = $"{uri.Scheme}://{uri.Host}:{uri.Port}";

        builder.Services.AddHttpClient("HeartbeatClient", client =>
        {
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(15);
        });

        // Servicios
        builder.Services.AddSingleton<INetworkMonitorService, NetworkMonitorService>();
        builder.Services.AddSingleton<IGeoLocationService, GeoLocationService>();
        builder.Services.AddSingleton<ILocalStorageService, LocalStorageService>();

        // HeartbeatService necesita IConfiguration e ILogger ahora
        builder.Services.AddSingleton<IHeartbeatService, HeartbeatService>();

        // Servicio de auto-descubrimiento UDP (encuentra al Comandante automáticamente)
        builder.Services.AddHostedService<CommanderDiscoveryService>();

        // Worker principal
        builder.Services.AddHostedService<AgentWorker>();

        var host = builder.Build();
        host.Run();
    }
}
