using Microsoft.AspNetCore.Mvc;
using SecurityMonitor.Commander.Services;
using SecurityMonitor.Shared.Models;
using SecurityMonitor.Commander.Desktop;

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
        public IActionResult Post([FromBody] HeartbeatDto heartbeat)
        {
            if (heartbeat == null || string.IsNullOrEmpty(heartbeat.MachineName))
            {
                return BadRequest("Invalid heartbeat data.");
            }

            _registryService.RegisterHeartbeat(heartbeat);
            StaticLog.LogHeartbeat(heartbeat);
            return Ok();
        }
    }
}
