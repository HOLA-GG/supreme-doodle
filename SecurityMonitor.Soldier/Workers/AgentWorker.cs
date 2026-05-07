using SecurityMonitorAgent.Models;
using SecurityMonitorAgent.Services.Interfaces;
using SecurityMonitor.Shared.Helpers;

namespace SecurityMonitorAgent.Workers;

public class AgentWorker : BackgroundService
{
    private readonly ILogger<AgentWorker> _logger;
    private readonly INetworkMonitorService _networkMonitor;
    private readonly IGeoLocationService _geoLocation;
    private readonly IHeartbeatService _heartbeatService;
    private readonly ILocalStorageService _localStorage;
    private readonly IConfiguration _configuration;

    public AgentWorker(
        ILogger<AgentWorker> logger,
        INetworkMonitorService networkMonitor,
        IGeoLocationService geoLocation,
        IHeartbeatService heartbeatService,
        ILocalStorageService localStorage,
        IConfiguration configuration)
    {
        _logger = logger;
        _networkMonitor = networkMonitor;
        _geoLocation = geoLocation;
        _heartbeatService = heartbeatService;
        _localStorage = localStorage;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Security Monitor Agent starting at: {time}", DateTimeOffset.Now);

        // Esperar 30 segundos al inicio para que el sistema de red esté listo
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        // Usar 30 segundos por defecto para una respuesta más rápida
        var intervalMinutes = _configuration.GetValue<double>("AgentSettings:HeartbeatIntervalMinutes", 0.5);
        
        // --- DESENCRIPTACIÓN DE CONFIGURACIÓN (CISCO PROTOCOLS) ---
        var rawAuthorizedNetwork = _configuration.GetValue<string>("AgentSettings:AuthorizedNetwork", "") ?? "";
        var authorizedNetwork = CryptoService.Decrypt(rawAuthorizedNetwork);
        var accountId = _configuration.GetValue<string>("AgentSettings:AccountId", "DEFAULT_USER") ?? "DEFAULT_USER";
        
        var rawLat = _configuration.GetValue<string>("AgentSettings:OriginLatitude", "0") ?? "0";
        var rawLon = _configuration.GetValue<string>("AgentSettings:OriginLongitude", "0") ?? "0";
        
        var decryptedLat = CryptoService.Decrypt(rawLat);
        var decryptedLon = CryptoService.Decrypt(rawLon);

        // --- DETECCIÓN DE MANIPULACIÓN ---
        if (decryptedLat == "ERROR_DECRYPTION" || decryptedLon == "ERROR_DECRYPTION")
        {
            _logger.LogCritical("¡ALERTA DE SEGURIDAD! El archivo de configuración ha sido manipulado o movido.");
            await SendAlarmHeartbeat("Security Alert: Config Tampering Detected");
            return; // Bloquear ejecución hasta que un admin lo corrija
        }

        double.TryParse(decryptedLat, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var originLat);
        double.TryParse(decryptedLon, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var originLon);

        var radiusMeters = _configuration.GetValue<double>("AgentSettings:AllowedRadiusMeters", 500);

        // AUTO-CONFIGURAR UBICACIÓN SI ES LA PRIMERA VEZ (0,0)
        if (originLat == 0 && originLon == 0)
        {
            _logger.LogInformation("Coordenadas base no detectadas. Capturando ubicación actual como 'Zona Segura' permanente...");
            var initialLoc = await _geoLocation.GetCurrentLocationAsync();
            if (initialLoc.Latitude != 0 || initialLoc.Longitude != 0)
            {
                originLat = initialLoc.Latitude;
                originLon = initialLoc.Longitude;
                _logger.LogInformation($"Sede base fijada en: {originLat}, {originLon}. Guardando en appsettings.json...");
                try
                {
                    string appSettingsPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
                    if (System.IO.File.Exists(appSettingsPath))
                    {
                        string json = System.IO.File.ReadAllText(appSettingsPath);
                        
                        // Encriptar antes de guardar
                        string encLat = CryptoService.Encrypt(originLat.ToString(System.Globalization.CultureInfo.InvariantCulture));
                        string encLon = CryptoService.Encrypt(originLon.ToString(System.Globalization.CultureInfo.InvariantCulture));

                        json = System.Text.RegularExpressions.Regex.Replace(json, @"""OriginLatitude""\s*:\s*[^,}]+", $@"""OriginLatitude"": ""{encLat}""");
                        json = System.Text.RegularExpressions.Regex.Replace(json, @"""OriginLongitude""\s*:\s*[^,}]+", $@"""OriginLongitude"": ""{encLon}""");
                        System.IO.File.WriteAllText(appSettingsPath, json);
                    }
                }
                catch (Exception ex) { _logger.LogWarning($"No se pudo actualizar appsettings.json de forma permanente: {ex.Message}"); }
            }
        }

        _logger.LogInformation("Configurado para monitorear red: '{Network}', Intervalo: {Interval} min", 
            authorizedNetwork, intervalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Obtener SSID actual real
                var currentSsid = _networkMonitor.GetCurrentSsid();
                var isAuthorizedNet = string.IsNullOrEmpty(authorizedNetwork) || 
                    currentSsid.Equals(authorizedNetwork, StringComparison.OrdinalIgnoreCase) ||
                    _networkMonitor.IsInAuthorizedNetwork(authorizedNetwork);

                var location = await _geoLocation.GetCurrentLocationAsync();
                var isOutsideGeo = _geoLocation.IsOutsideGeofence(
                    location.Latitude, location.Longitude, originLat, originLon, radiusMeters, isAuthorizedNet);

                bool isAlarmActive = !isAuthorizedNet || isOutsideGeo;
                string alarmReason = "";
                if (!isAuthorizedNet) alarmReason += "Unauthorized Network. ";
                if (isOutsideGeo) alarmReason += "Outside Geofence.";

                var payload = new HeartbeatPayload
                {
                    AccountId = accountId,
                    Hostname = Environment.MachineName,
                    MacAddress = _networkMonitor.GetMacAddress(),
                    LocalIp = _networkMonitor.GetLocalIpAddress(),
                    ConnectionType = _networkMonitor.GetActiveConnectionType(),
                    CurrentSsid = currentSsid,
                    Latitude = location.Latitude,
                    Longitude = location.Longitude,
                    IsAlarmActive = isAlarmActive,
                    AlarmReason = isAlarmActive ? alarmReason.Trim() : null,
                    Timestamp = DateTime.UtcNow
                };

                _logger.LogInformation("Ciclo de monitoreo: IP={Ip}, Red={Ssid}, Autorizado={Auth}, Enviando heartbeat...",
                    payload.LocalIp, currentSsid, isAuthorizedNet);

                // Intentar enviar directamente (sin guardar en DB primero para simplificar)
                var sent = await _heartbeatService.SendHeartbeatAsync(payload);
                if (sent)
                {
                    _logger.LogInformation("Heartbeat enviado correctamente al Comandante.");
                }
                else
                {
                    // Si falla el envío, guardar localmente para reintentar
                    _localStorage.SaveAlert(payload);
                    _logger.LogWarning("No se pudo contactar al Comandante. Guardando localmente.");
                    
                    // Reintentar los pendientes
                    var pendings = _localStorage.GetPendingAlerts();
                    foreach (var pending in pendings)
                    {
                        var retrySent = await _heartbeatService.SendHeartbeatAsync(pending.Payload);
                        if (retrySent)
                        {
                            _localStorage.MarkAsSent(pending.Id);
                            _logger.LogInformation("Heartbeat pendiente enviado. ID={Id}", pending.Id);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ejecutando ciclo del worker.");
            }

            await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), stoppingToken);
        }
    }

    private async Task SendAlarmHeartbeat(string reason)
    {
        try
        {
            var accountId = _configuration.GetValue<string>("AgentSettings:AccountId", "DEFAULT_USER") ?? "DEFAULT_USER";
            var payload = new HeartbeatPayload
            {
                AccountId = accountId,
                Hostname = Environment.MachineName,
                MacAddress = _networkMonitor.GetMacAddress(),
                LocalIp = _networkMonitor.GetLocalIpAddress(),
                ConnectionType = _networkMonitor.GetActiveConnectionType(),
                CurrentSsid = _networkMonitor.GetCurrentSsid(),
                Latitude = 0,
                Longitude = 0,
                IsAlarmActive = true,
                AlarmReason = reason,
                Timestamp = DateTime.UtcNow
            };
            await _heartbeatService.SendHeartbeatAsync(payload);
        }
        catch { /* No dejar que un fallo aquí detenga el flujo principal */ }
    }
}
