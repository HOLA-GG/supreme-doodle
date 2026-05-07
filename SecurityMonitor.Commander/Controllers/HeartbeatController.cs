using Microsoft.AspNetCore.Mvc;
using SecurityMonitor.Commander.Services;
using SecurityMonitor.Shared.Models;
using SecurityMonitor.Shared.Helpers;
using System.Text.Json;

namespace SecurityMonitor.Commander.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HeartbeatController : ControllerBase
    {
        private readonly SoldierRegistryService _registryService;

        public HeartbeatController(SoldierRegistryService registryService)
        {
            _registryService = registryService;
        }

        [HttpPost]
        public IActionResult Post([FromBody] SecurePayload payload)
        {
            if (payload == null || string.IsNullOrEmpty(payload.Data))
            {
                return BadRequest("Invalid secure payload.");
            }

            // 1. Verificar integridad (HMAC-SHA256) - Estilo Cisco
            var expectedSignature = CryptoService.GenerateHmac(payload.Data);
            if (payload.Signature != expectedSignature)
            {
                return Unauthorized("Firma de seguridad no válida (Posible manipulación de tráfico).");
            }

            // 2. Prevenir Ataque de Replay (Anti-Replay)
            var nowTicks = DateTime.UtcNow.Ticks;
            var timeDiff = TimeSpan.FromTicks(Math.Abs(nowTicks - payload.TimestampTicks));
            if (timeDiff.TotalMinutes > 5)
            {
                return Unauthorized("Mensaje expirado (Ataque de Replay detectado).");
            }

            // 3. Desencriptar (AES-256)
            try
            {
                var json = CryptoService.DecryptTraffic(payload.Data);
                var heartbeat = JsonSerializer.Deserialize<HeartbeatDto>(json);

                if (heartbeat == null || string.IsNullOrEmpty(heartbeat.MachineName))
                {
                    return BadRequest("Datos desencriptados inválidos.");
                }

                _registryService.RegisterHeartbeat(heartbeat);
                return Ok();
            }
            catch (Exception)
            {
                return Unauthorized("Error al desencriptar el paquete (Clave incorrecta).");
            }
        }
    }
}
