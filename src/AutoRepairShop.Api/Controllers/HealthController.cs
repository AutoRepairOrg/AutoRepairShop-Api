using AutoRepairShop.Application.DTOs.Auth;
using AutoRepairShop.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace AutoRepairShop.Api.Controllers
{
    [ApiController]
    [Route("health")]
    public class HealthController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get()
        {
            return Ok(new
            {
                status = "healthy",
                service = "AutoRepairShop.Api",
                timestamp = DateTime.UtcNow
            });
        }
    }
}