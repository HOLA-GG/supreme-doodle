using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SecurityMonitor.Commander.Services;
using System.Text;

var options = new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
};
var builder = WebApplication.CreateBuilder(options);

builder.Host.UseWindowsService();

// Forzar el puerto a 5000 para el Dashboard
builder.WebHost.UseUrls("http://0.0.0.0:5000");

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddSingleton<SoldierRegistryService>();
builder.Services.AddHostedService<SecurityMonitor.Commander.Services.BroadcastService>();

var app = builder.Build();

app.MapControllers();

// Dashboard HTML Premium (Dark Mode - Estilo Centro de Comando Militar)
app.MapGet("/", (SoldierRegistryService registry) =>
{
    var soldiers = registry.GetAllSoldiers();
    var authorizedNet = builder.Configuration.GetValue<string>("AgentSettings:AuthorizedNetwork") ?? "No configurada";
    
    int online = soldiers.Count(s => (DateTime.UtcNow - s.LastSeen).TotalMinutes <= 5 && s.IsOnAuthorizedNetwork);
    int alerts = soldiers.Count(s => (DateTime.UtcNow - s.LastSeen).TotalMinutes <= 5 && !s.IsOnAuthorizedNetwork);
    int offline = soldiers.Count(s => (DateTime.UtcNow - s.LastSeen).TotalMinutes > 5);
    
    var sb = new StringBuilder();
    sb.AppendLine("<!DOCTYPE html><html lang='es'><head><meta charset='UTF-8'><title>Security Monitor ─ Centro de Comando</title>");
    sb.AppendLine("<meta http-equiv=\"refresh\" content=\"5\">");
    sb.AppendLine("<link rel='stylesheet' href='https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.5.1/css/all.min.css'>");
    sb.AppendLine("<link href='https://fonts.googleapis.com/css2?family=Inter:wght@300;400;500;600;700&display=swap' rel='stylesheet'>");
    sb.AppendLine("<style>");
    
    // === CSS Variables & Base ===
    sb.AppendLine(@"
      :root {
        --bg-primary: #0a0e17; --bg-secondary: #111827; --bg-card: #1a1f2e;
        --border: #2a3042; --border-glow: rgba(59,130,246,0.3);
        --text-primary: #e2e8f0; --text-secondary: #94a3b8; --text-muted: #64748b;
        --accent: #3b82f6; --accent-glow: rgba(59,130,246,0.15);
        --success: #10b981; --success-bg: rgba(16,185,129,0.1);
        --danger: #ef4444; --danger-bg: rgba(239,68,68,0.1);
        --warning: #f59e0b; --warning-bg: rgba(245,158,11,0.1);
        --offline-color: #475569;
      }
      * { margin: 0; padding: 0; box-sizing: border-box; }
      body { font-family: 'Inter', sans-serif; background: var(--bg-primary); color: var(--text-primary); min-height: 100vh; }
    ");

    // === Header / Navbar ===
    sb.AppendLine(@"
      .navbar { background: var(--bg-secondary); border-bottom: 1px solid var(--border); padding: 12px 30px; display: flex; justify-content: space-between; align-items: center; }
      .navbar .brand { display: flex; align-items: center; gap: 12px; }
      .navbar .brand i { font-size: 24px; color: var(--accent); }
      .navbar .brand h1 { font-size: 18px; font-weight: 600; letter-spacing: -0.3px; }
      .navbar .brand span { color: var(--accent); }
      .navbar .meta { display: flex; align-items: center; gap: 20px; font-size: 13px; color: var(--text-secondary); }
      .navbar .meta .net-badge { background: var(--success-bg); color: var(--success); padding: 4px 12px; border-radius: 20px; font-weight: 500; border: 1px solid rgba(16,185,129,0.2); }
      .navbar .nav-links a { color: var(--text-secondary); text-decoration: none; padding: 8px 16px; border-radius: 6px; font-size: 13px; font-weight: 500; transition: all 0.2s; }
      .navbar .nav-links a:hover { background: var(--accent-glow); color: var(--accent); }
    ");

    // === Stats Cards ===
    sb.AppendLine(@"
      .stats-bar { display: grid; grid-template-columns: repeat(4, 1fr); gap: 16px; padding: 20px 30px; }
      .stat-card { background: var(--bg-card); border: 1px solid var(--border); border-radius: 10px; padding: 18px 20px; display: flex; align-items: center; gap: 16px; transition: border-color 0.3s; }
      .stat-card:hover { border-color: var(--border-glow); }
      .stat-card .stat-icon { width: 48px; height: 48px; border-radius: 10px; display: flex; align-items: center; justify-content: center; font-size: 20px; }
      .stat-card .stat-icon.total { background: var(--accent-glow); color: var(--accent); }
      .stat-card .stat-icon.online { background: var(--success-bg); color: var(--success); }
      .stat-card .stat-icon.alert { background: var(--danger-bg); color: var(--danger); }
      .stat-card .stat-icon.offline { background: rgba(71,85,105,0.15); color: var(--offline-color); }
      .stat-card .stat-info h3 { font-size: 24px; font-weight: 700; }
      .stat-card .stat-info p { font-size: 12px; color: var(--text-muted); text-transform: uppercase; letter-spacing: 0.5px; margin-top: 2px; }
    ");

    // === Table ===
    sb.AppendLine(@"
      .content { padding: 0 30px 30px; }
      .table-container { background: var(--bg-card); border: 1px solid var(--border); border-radius: 10px; overflow: hidden; }
      .table-header { padding: 16px 20px; border-bottom: 1px solid var(--border); display: flex; justify-content: space-between; align-items: center; }
      .table-header h2 { font-size: 15px; font-weight: 600; display: flex; align-items: center; gap: 8px; }
      .table-header h2 i { color: var(--accent); }
      .table-header .security-badge { font-size: 11px; background: var(--success-bg); color: var(--success); padding: 4px 10px; border-radius: 20px; border: 1px solid rgba(16,185,129,0.2); }
      table { width: 100%; border-collapse: collapse; }
      thead th { background: rgba(59,130,246,0.05); padding: 10px 16px; text-align: left; font-size: 11px; text-transform: uppercase; letter-spacing: 0.5px; color: var(--text-muted); font-weight: 600; border-bottom: 1px solid var(--border); }
      tbody td { padding: 12px 16px; border-bottom: 1px solid var(--border); font-size: 13px; vertical-align: middle; }
      tbody tr { transition: background 0.15s; }
      tbody tr:hover { background: rgba(59,130,246,0.03); }
      tbody tr:last-child td { border-bottom: none; }
    ");

    // === Badges & Utils ===
    sb.AppendLine(@"
      .badge { display: inline-flex; align-items: center; gap: 5px; padding: 3px 10px; border-radius: 20px; font-size: 11px; font-weight: 600; }
      .badge.online { background: var(--success-bg); color: var(--success); border: 1px solid rgba(16,185,129,0.2); }
      .badge.alert { background: var(--danger-bg); color: var(--danger); border: 1px solid rgba(239,68,68,0.2); animation: pulse-danger 2s infinite; }
      .badge.offline { background: rgba(71,85,105,0.1); color: var(--offline-color); border: 1px solid rgba(71,85,105,0.2); }
      .badge.warning { background: var(--warning-bg); color: var(--warning); border: 1px solid rgba(245,158,11,0.2); }
      .mac-label { font-family: 'Courier New', monospace; font-size: 11px; color: var(--text-muted); background: rgba(59,130,246,0.05); padding: 2px 8px; border-radius: 4px; }
      .fingerprint { font-family: 'Courier New', monospace; font-size: 10px; color: var(--text-muted); max-width: 100px; overflow: hidden; text-overflow: ellipsis; display: inline-block; }
      .device-icon { font-size: 16px; margin-right: 6px; }
      .empty-state { text-align: center; padding: 80px 20px; color: var(--text-muted); }
      .empty-state i { font-size: 48px; margin-bottom: 16px; opacity: 0.3; }
      .dot { width: 8px; height: 8px; border-radius: 50%; display: inline-block; margin-right: 6px; }
      .dot.green { background: var(--success); box-shadow: 0 0 6px var(--success); }
      .dot.red { background: var(--danger); box-shadow: 0 0 6px var(--danger); animation: pulse-dot 1.5s infinite; }
      .dot.gray { background: var(--offline-color); }
      @keyframes pulse-danger { 0%,100% { box-shadow: 0 0 0 rgba(239,68,68,0); } 50% { box-shadow: 0 0 12px rgba(239,68,68,0.3); } }
      @keyframes pulse-dot { 0%,100% { opacity: 1; } 50% { opacity: 0.4; } }
      .net-info { display: flex; align-items: center; gap: 6px; }
      .time-ago { color: var(--text-muted); font-size: 12px; }
    ");

    sb.AppendLine("</style></head><body>");

    // === Navbar ===
    sb.AppendLine("<div class='navbar'>");
    sb.AppendLine("  <div class='brand'><i class='fas fa-shield-halved'></i><h1>Security <span>Monitor</span></h1></div>");
    sb.AppendLine($"  <div class='meta'><span class='net-badge'><i class='fas fa-wifi'></i> {authorizedNet}</span>");
    sb.AppendLine($"    <span><i class='fas fa-lock'></i> AES-256 + HMAC</span></div>");
    sb.AppendLine("  <div class='nav-links'><a href='/settings'><i class='fas fa-cog'></i> Configuración</a></div>");
    sb.AppendLine("</div>");

    // === Stats Bar ===
    sb.AppendLine("<div class='stats-bar'>");
    sb.AppendLine($"  <div class='stat-card'><div class='stat-icon total'><i class='fas fa-server'></i></div><div class='stat-info'><h3>{soldiers.Count}</h3><p>Total Soldados</p></div></div>");
    sb.AppendLine($"  <div class='stat-card'><div class='stat-icon online'><i class='fas fa-circle-check'></i></div><div class='stat-info'><h3>{online}</h3><p>En Línea</p></div></div>");
    sb.AppendLine($"  <div class='stat-card'><div class='stat-icon alert'><i class='fas fa-triangle-exclamation'></i></div><div class='stat-info'><h3>{alerts}</h3><p>En Alerta</p></div></div>");
    sb.AppendLine($"  <div class='stat-card'><div class='stat-icon offline'><i class='fas fa-power-off'></i></div><div class='stat-info'><h3>{offline}</h3><p>Desconectados</p></div></div>");
    sb.AppendLine("</div>");

    // === Table ===
    sb.AppendLine("<div class='content'><div class='table-container'>");
    sb.AppendLine("<div class='table-header'><h2><i class='fas fa-table-list'></i> Soldados Registrados</h2>");
    sb.AppendLine("<span class='security-badge'><i class='fas fa-fingerprint'></i> ID por MAC de Fábrica</span></div>");
    
    if (soldiers.Count > 0)
    {
        sb.AppendLine("<table><thead><tr>");
        sb.AppendLine("<th>Estado</th><th>Equipo</th><th>Usuario</th><th>MAC (Fábrica)</th><th>IP</th><th>Red</th><th>Seguridad</th><th>Último Reporte</th>");
        sb.AppendLine("</tr></thead><tbody>");

        foreach (var s in soldiers)
        {
            var timeSince = DateTime.UtcNow - s.LastSeen;
            bool isOff = timeSince.TotalMinutes > 5;
            bool hasAlert = !s.IsOnAuthorizedNetwork;
            
            string lastSeenText = timeSince.TotalSeconds < 90 ? "Hace segundos" : $"Hace {(int)timeSince.TotalMinutes} min";
            
            string dotClass, badgeClass, badgeText;
            if (!isOff && !hasAlert) { dotClass = "green"; badgeClass = "online"; badgeText = "En Línea"; }
            else if (!isOff && hasAlert) { dotClass = "red"; badgeClass = "alert"; badgeText = "⚠ ALERTA"; }
            else if (isOff && s.LastSeen != default) { dotClass = "red"; badgeClass = "alert"; badgeText = "SIN SEÑAL"; }
            else { dotClass = "gray"; badgeClass = "offline"; badgeText = "Offline"; }
            
            string devIcon = s.ConnectionType == "WiFi" ? "fa-laptop" : "fa-desktop";
            string netIcon = s.ConnectionType == "WiFi" ? "fa-wifi" : "fa-network-wired";
            string netInfo = s.ConnectionType == "WiFi" 
                ? (string.IsNullOrEmpty(s.CurrentNetworkSsid) || s.CurrentNetworkSsid == "N/A" ? "Detectando..." : s.CurrentNetworkSsid) 
                : "Ethernet";
            
            // Formatear MAC con separadores
            string macFormatted = s.MacAddress.Length >= 12 
                ? string.Join(":", Enumerable.Range(0, 6).Select(i => s.MacAddress.Substring(i * 2, 2)))
                : s.MacAddress;
            
            var netColor = s.IsOnAuthorizedNetwork ? "var(--success)" : "var(--danger)";
            var secIcon = s.IsOnAuthorizedNetwork ? "fa-shield-check" : "fa-shield-xmark";
            var secText = s.IsOnAuthorizedNetwork ? "Autorizada" : "NO Autorizada";
            var secBadge = s.IsOnAuthorizedNetwork ? "online" : "alert";

            sb.AppendLine("<tr>");
            sb.AppendLine($"<td><span class='dot {dotClass}'></span><span class='badge {badgeClass}'>{badgeText}</span></td>");
            sb.AppendLine($"<td><i class='fas {devIcon} device-icon' style='color:var(--accent)'></i><strong>{s.MachineName}</strong></td>");
            sb.AppendLine($"<td><i class='fas fa-user' style='color:var(--text-muted);margin-right:4px'></i>{s.UserName}</td>");
            sb.AppendLine($"<td><span class='mac-label'>{macFormatted}</span></td>");
            sb.AppendLine($"<td>{s.IpAddress}</td>");
            sb.AppendLine($"<td><div class='net-info'><i class='fas {netIcon}' style='color:var(--text-muted)'></i> {netInfo}</div></td>");
            sb.AppendLine($"<td><span class='badge {secBadge}'><i class='fas {secIcon}'></i> {secText}</span></td>");
            sb.AppendLine($"<td><span class='time-ago'><i class='fas fa-clock'></i> {lastSeenText}</span></td>");
            sb.AppendLine("</tr>");
        }
        sb.AppendLine("</tbody></table>");
    }
    else
    {
        sb.AppendLine("<div class='empty-state'>");
        sb.AppendLine("  <i class='fas fa-satellite-dish'></i>");
        sb.AppendLine("  <p>Esperando conexión de soldados...</p>");
        sb.AppendLine("  <p style='font-size:12px;margin-top:8px'>Instala el agente en los equipos remotos. Los soldados se descubrirán automáticamente por UDP.</p>");
        sb.AppendLine("</div>");
    }
    
    sb.AppendLine("</div></div></body></html>");
    return Results.Content(sb.ToString(), "text/html", Encoding.UTF8);
});


// Fase 2: Panel de Configuración (Boceto)
app.MapGet("/settings", () =>
{
    var sb = new StringBuilder();
    sb.AppendLine("<!DOCTYPE html><html lang='es'><head><meta charset='UTF-8'><title>Configuración - Comandante</title>");
    sb.AppendLine("<link rel='stylesheet' href='https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.4.0/css/all.min.css'>");
    sb.AppendLine("<style>");
    sb.AppendLine("  body { font-family: 'Segoe UI', sans-serif; background: #f0f2f5; padding: 40px; color: #2c3e50; }");
    sb.AppendLine("  .container { background: white; padding: 30px; border-radius: 12px; box-shadow: 0 4px 10px rgba(0,0,0,0.1); max-width: 800px; margin: 0 auto; }");
    sb.AppendLine("  h2 { border-bottom: 2px solid #3498db; padding-bottom: 10px; }");
    sb.AppendLine("  .form-group { margin-bottom: 20px; }");
    sb.AppendLine("  label { display: block; font-weight: bold; margin-bottom: 5px; }");
    sb.AppendLine("  input { width: 100%; padding: 10px; border: 1px solid #ccc; border-radius: 5px; }");
    sb.AppendLine("  .btn { padding: 10px 20px; background: #3498db; color: white; border: none; border-radius: 5px; cursor: pointer; font-size: 1em; }");
    sb.AppendLine("  .btn:hover { background: #2980b9; }");
    sb.AppendLine("</style>");
    sb.AppendLine("</head><body>");
    sb.AppendLine("<div class='container'>");
    sb.AppendLine("  <a href='/' style='color: #3498db; text-decoration: none;'><i class='fas fa-arrow-left'></i> Volver al Dashboard</a>");
    sb.AppendLine("  <h2><i class='fas fa-map-location-dot'></i> Configuración de Geocerca (En Desarrollo)</h2>");
    sb.AppendLine("  <p>Aquí definiremos la ubicación central permitida para todos los equipos.</p>");
    sb.AppendLine("  <div class='form-group'><label>Latitud Base:</label><input type='text' value='0' disabled></div>");
    sb.AppendLine("  <div class='form-group'><label>Longitud Base:</label><input type='text' value='0' disabled></div>");
    sb.AppendLine("  <div class='form-group'><label>Radio Permitido (metros):</label><input type='number' value='500' disabled></div>");
    sb.AppendLine("  <div style='background: #e8f5e9; padding: 20px; border-radius: 8px; text-align: center; border: 1px dashed #2ecc71;'>");
    sb.AppendLine("    <i class='fas fa-map' style='font-size: 3em; color: #2ecc71; margin-bottom: 10px;'></i><br>");
    sb.AppendLine("    Aquí irá el Mapa Interactivo (OpenStreetMap) para seleccionar el punto.");
    sb.AppendLine("  </div><br>");
    sb.AppendLine("  <button class='btn' disabled>Guardar Configuración</button>");
    sb.AppendLine("</div></body></html>");
    return Results.Content(sb.ToString(), "text/html", Encoding.UTF8);
});

app.Run();
