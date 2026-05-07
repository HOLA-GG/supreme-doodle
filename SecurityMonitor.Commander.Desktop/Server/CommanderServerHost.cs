using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SecurityMonitor.Commander.Services;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SecurityMonitor.Commander.Desktop.Server
{
    public class CommanderServerHost
    {
        private IHost? _host;
        public string ServerUrl { get; } = "http://0.0.0.0:5000";
        public string AuthorizedSsid { get; set; } = "FUS-ADMIN";
        public double BaseLatitude { get; set; } = 4.60971;
        public double BaseLongitude { get; set; } = -74.08175;
        public double AllowedRadius { get; set; } = 500;
        public bool MuteAlerts { get; set; } = false;

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            var builder = WebApplication.CreateBuilder();

            // Configurar el puerto
            builder.WebHost.UseUrls(ServerUrl);

            // Add services to the container.
            builder.Services.AddControllers();
            builder.Services.AddSingleton<SoldierRegistryService>();
            builder.Services.AddHostedService<BroadcastService>();

            var app = builder.Build();

            app.MapControllers();

            // Re-usar la lógica del Dashboard que estaba en Program.cs
            app.MapGet("/", (SoldierRegistryService registry) =>
            {
                var soldiers = registry.GetAllSoldiers();
                var authorizedNet = AuthorizedSsid; 
                
                var sb = new StringBuilder();
                sb.AppendLine("<!DOCTYPE html><html lang='es'><head><meta charset='UTF-8'><title>Comandante - Monitor de Seguridad</title>");
                sb.AppendLine("<meta http-equiv=\"refresh\" content=\"5\">");
                sb.AppendLine("<link rel='stylesheet' href='https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.4.0/css/all.min.css'>");
                sb.AppendLine("<style>");
                sb.AppendLine("  :root { --bg: #0f172a; --card-bg: #1e293b; --primary: #38bdf8; --success: #22c55e; --danger: #ef4444; --warning: #f59e0b; --offline: #64748b; --text: #f8fafc; }");
                sb.AppendLine("  body { font-family: 'Inter', 'Segoe UI', sans-serif; background-color: var(--bg); margin: 0; padding: 20px; color: var(--text); }");
                sb.AppendLine("  header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 30px; border-bottom: 1px solid #334155; padding-bottom: 20px; }");
                sb.AppendLine("  .grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(280px, 1fr)); gap: 24px; }");
                sb.AppendLine("  .card { background: var(--card-bg); border-radius: 16px; padding: 24px; box-shadow: 0 10px 15px -3px rgba(0,0,0,0.1); transition: all 0.3s cubic-bezier(0.4, 0, 0.2, 1); text-align: left; border: 1px solid #334155; position: relative; overflow: hidden; }");
                sb.AppendLine("  .card::before { content: ''; position: absolute; top: 0; left: 0; width: 4px; height: 100%; background: var(--offline); }");
                sb.AppendLine("  .card:hover { transform: translateY(-4px); box-shadow: 0 20px 25px -5px rgba(0,0,0,0.2); border-color: var(--primary); }");
                sb.AppendLine("  .card.online::before { background: var(--success); }");
                sb.AppendLine("  .card.alert::before { background: var(--danger); }");
                sb.AppendLine("  .card.warning::before { background: var(--warning); }");
                sb.AppendLine("  .card.alert { animation: pulse-border 2s infinite; }");
                sb.AppendLine("  @keyframes pulse-border { 0% { border-color: #334155; } 50% { border-color: var(--danger); } 100% { border-color: #334155; } }");
                sb.AppendLine("  .card-header { display: flex; justify-content: space-between; align-items: flex-start; margin-bottom: 20px; }");
                sb.AppendLine("  .icon-box { width: 48px; height: 48px; border-radius: 12px; display: flex; align-items: center; justify-content: center; font-size: 24px; background: #334155; }");
                sb.AppendLine("  .machine-info { flex-grow: 1; margin-left: 16px; }");
                sb.AppendLine("  .machine-name { font-weight: 700; font-size: 1.2em; display: block; color: var(--text); }");
                sb.AppendLine("  .user-name { color: #94a3b8; font-size: 0.9em; }");
                sb.AppendLine("  .status-badge { display: inline-block; padding: 4px 12px; border-radius: 9999px; font-size: 0.75em; text-transform: uppercase; font-weight: 700; margin-top: 8px; }");
                sb.AppendLine("  .card.online .status-badge { background: rgba(34, 197, 94, 0.1); color: var(--success); }");
                sb.AppendLine("  .card.alert .status-badge { background: rgba(239, 68, 68, 0.1); color: var(--danger); }");
                sb.AppendLine("  .card.warning .status-badge { background: rgba(245, 158, 11, 0.1); color: var(--warning); }");
                sb.AppendLine("  .card.offline .status-badge { background: rgba(100, 116, 139, 0.1); color: var(--offline); }");
                sb.AppendLine("  .details { font-size: 0.85em; margin-top: 20px; color: #cbd5e1; }");
                sb.AppendLine("  .detail-item { display: flex; align-items: center; margin-bottom: 10px; }");
                sb.AppendLine("  .detail-item i { width: 20px; color: var(--primary); margin-right: 10px; }");
                sb.AppendLine("  .location-link { color: var(--primary); text-decoration: none; font-weight: 600; }");
                sb.AppendLine("  .location-link:hover { text-decoration: underline; }");
                sb.AppendLine("  .net-tag { font-size: 0.7em; padding: 2px 6px; border-radius: 4px; background: #475569; margin-left: 8px; vertical-align: middle; }");
                sb.AppendLine("</style>");
                sb.AppendLine("</head><body>");
                sb.AppendLine("<header>");
                sb.AppendLine($"<div><span style='background:#1e293b; padding:10px 20px; border-radius:12px; border:1px solid #334155;'><i class='fas fa-network-wired'></i> Red: <strong style='color:var(--success)'>{authorizedNet}</strong> | <i class='fas fa-microchip'></i> Agentes: <strong>{soldiers.Count}</strong></span></div></header>");
                
                sb.AppendLine("<div class='grid'>");
                
                foreach (var s in soldiers)
                {
                    var timeSinceLastSeen = System.DateTime.UtcNow - s.LastSeen.ToUniversalTime();
                    bool isCriticalOffline = timeSinceLastSeen.TotalMinutes >= 30;
                    bool isOffline = timeSinceLastSeen.TotalMinutes >= 5 && !isCriticalOffline;
                    bool hasAlert = !s.IsOnAuthorizedNetwork || isCriticalOffline;
                    
                    string statusClass = hasAlert ? "alert" : (isOffline ? "warning" : "online");
                    string statusText = hasAlert ? (isCriticalOffline ? "ALERTA: PERDIDO" : "ALERTA DE RED") : (isOffline ? "Apagado / Sin Red" : "Protegido");
                    string iconColor = hasAlert ? "var(--danger)" : (isOffline ? "var(--warning)" : "var(--success)");
                    
                    string deviceIcon = s.ConnectionType == "WiFi" ? "fa-laptop" : "fa-desktop";
                    string netIcon = s.ConnectionType == "WiFi" ? "fa-wifi" : "fa-ethernet";
                    string netLabel = s.ConnectionType == "WiFi" ? s.CurrentNetworkSsid : "Cable Ethernet";
                    if (string.IsNullOrEmpty(netLabel) || netLabel == "N/A") netLabel = "Desconocida";

                    sb.AppendLine($"<div class='card {statusClass}'>");
                    sb.AppendLine("  <div class='card-header'>");
                    sb.AppendLine($"    <div class='icon-box' style='color: {iconColor}'><i class='fas {deviceIcon}'></i></div>");
                    sb.AppendLine("    <div class='machine-info'>");
                    sb.AppendLine($"      <span class='machine-name'>{s.MachineName} <span class='net-tag'>{s.ConnectionType}</span></span>");
                    sb.AppendLine($"      <span class='user-name'><i class='fas fa-user'></i> {s.UserName}</span><br>");
                    sb.AppendLine($"      <span class='status-badge'>{statusText}</span>");
                    sb.AppendLine("    </div>");
                    sb.AppendLine("  </div>");
                    
                    sb.AppendLine("  <div class='details'>");
                    sb.AppendLine($"    <div class='detail-item'><i class='fas fa-plug'></i> IP: {s.IpAddress}</div>");
                    sb.AppendLine($"    <div class='detail-item'><i class='fas {netIcon}'></i> Red: {netLabel}</div>");
                    
                    if (s.Latitude != 0 || s.Longitude != 0)
                    {
                        var mapUrl = $"https://www.openstreetmap.org/?mlat={s.Latitude}&mlon={s.Longitude}#map=16/{s.Latitude}/{s.Longitude}";
                        sb.AppendLine($"    <div class='detail-item'><i class='fas fa-location-dot'></i> <a href='{mapUrl}' target='_blank' class='location-link'>Ver en Mapa</a></div>");
                    }
                    else
                    {
                        sb.AppendLine("    <div class='detail-item'><i class='fas fa-location-dot'></i> Ubicación no disponible</div>");
                    }

                    string timeText = timeSinceLastSeen.TotalSeconds < 60 ? "ahora mismo" : $"hace {(int)timeSinceLastSeen.TotalMinutes}m";
                    sb.AppendLine($"    <div class='detail-item' style='margin-top:15px; color:#64748b; font-size:0.8em;'><i class='fas fa-clock'></i> Último pulso: {timeText}</div>");
                    sb.AppendLine("  </div>");
                    sb.AppendLine("</div>");
                }
                
                if (soldiers.Count == 0)
                {
                    sb.AppendLine("<div style='grid-column: 1/-1; text-align: center; padding: 50px;'>");
                    sb.AppendLine("  <i class='fas fa-info-circle' style='font-size: 3em; color: #ccc;'></i>");
                    sb.AppendLine("  <p style='color: #999; margin-top: 10px;'>Esperando conexión de soldados...</p>");
                    sb.AppendLine("</div>");
                }

                sb.AppendLine("</div></body></html>");
                return Results.Content(sb.ToString(), "text/html", Encoding.UTF8);
            });

            _host = app;
            await _host.StartAsync(cancellationToken);
        }

        public async Task StopAsync()
        {
            if (_host != null)
            {
                await _host.StopAsync();
                _host.Dispose();
            }
        }
    }
}
