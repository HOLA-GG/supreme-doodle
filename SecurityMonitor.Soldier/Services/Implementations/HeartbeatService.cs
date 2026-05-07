using System.Net.Http.Json;
using System.Text.Json;
using SecurityMonitorAgent.Models;
using SecurityMonitorAgent.Services.Interfaces;
using SecurityMonitor.Shared.Models;
using SecurityMonitor.Shared.Helpers;

namespace SecurityMonitorAgent.Services.Implementations;

public class HeartbeatService : IHeartbeatService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<HeartbeatService> _logger;

    public HeartbeatService(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<HeartbeatService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<bool> SendHeartbeatAsync(HeartbeatPayload payload)
    {
        // 1. Intentar con URL descubierta automáticamente via UDP broadcast
        var discoveredUrl = CommanderDiscoveryService.DiscoveredCommanderUrl;
        
        // 2. Fallback: URL del appsettings.json
        var configUrl = _configuration.GetValue<string>("AgentSettings:HeartbeatEndpointUrl")
                        ?? "http://localhost:5000/api/heartbeat";

        // Determinar qué URL usar
        var targetBaseUrl = discoveredUrl ?? ExtractBaseUrl(configUrl);
        var endpointUrl = "/api/heartbeat";

        _logger.LogInformation("Enviando heartbeat a: {Base}{Path} (Fuente: {Source})",
            targetBaseUrl, endpointUrl,
            discoveredUrl != null ? "Auto-Descubrimiento UDP" : "appsettings.json");

        var dto = new HeartbeatDto
        {
            MachineName = payload.Hostname,
            UserName = Environment.UserName,
            MacAddress = CryptoService.GetPrimaryMacAddress(),
            HardwareFingerprint = CryptoService.GetHardwareFingerprint(),
            IpAddress = payload.LocalIp,
            ConnectionType = payload.ConnectionType,
            CurrentNetworkSsid = payload.CurrentSsid,
            Latitude = payload.Latitude,
            Longitude = payload.Longitude,
            IsInAllowedRadius = !(payload.AlarmReason?.Contains("Outside Geofence") ?? false),
            IsOnAuthorizedNetwork = !(payload.AlarmReason?.Contains("Unauthorized Network") ?? false),
            Timestamp = payload.Timestamp
        };

        // --- SEGURIDAD MÁXIMA (CISCO PROTOCOLS) ---
        var json = JsonSerializer.Serialize(dto);
        var encryptedData = CryptoService.EncryptTraffic(json);
        var securePayload = new SecurePayload
        {
            Data = encryptedData,
            Signature = CryptoService.GenerateHmac(encryptedData),
            MachineName = dto.MachineName,
            TimestampTicks = DateTime.UtcNow.Ticks
        };

        // Intentar primero con URL descubierta
        if (discoveredUrl != null)
        {
            var sent = await TrySend(targetBaseUrl, endpointUrl, securePayload);
            if (sent) return true;

            _logger.LogWarning("Falló URL descubierta, intentando con appsettings...");
        }

        // Fallback a configuración manual (puede tener múltiples URLs separadas por coma)
        if (configUrl == "AUTO_DISCOVERY" || string.IsNullOrWhiteSpace(configUrl)) return false;

        var urls = configUrl.Split(',', StringSplitOptions.RemoveEmptyEntries);
        bool anySuccess = false;
        
        foreach (var u in urls)
        {
            var cleanUrl = ExtractBaseUrl(u.Trim());
            var success = await TrySend(cleanUrl, endpointUrl, securePayload);
            if (success) anySuccess = true;
        }
        
        return anySuccess;
    }

    private async Task<bool> TrySend(string baseUrl, string path, SecurePayload securePayload)
    {
        try
        {
            using var httpClient = new HttpClient
            {
                BaseAddress = new Uri(baseUrl),
                Timeout = TimeSpan.FromSeconds(10)
            };
            var response = await httpClient.PostAsJsonAsync(path, securePayload);
            _logger.LogInformation("Respuesta del Comandante: HTTP {Status} (Packet Encrypted & Signed)", (int)response.StatusCode);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Error conectando a {Url}: {Error}", baseUrl, ex.Message);
            return false;
        }
    }

    private static string ExtractBaseUrl(string fullUrl)
    {
        try
        {
            var uri = new Uri(fullUrl);
            return $"{uri.Scheme}://{uri.Host}:{uri.Port}";
        }
        catch
        {
            return "http://localhost:5000";
        }
    }
}
