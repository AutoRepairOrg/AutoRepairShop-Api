using System.Security.Claims;
using AutoRepairShop.Application.DTOs.ServiceOrder.Request;
using AutoRepairShop.Application.Interfaces.Services;
using AutoRepairShop.Domain.Enums;
using AutoRepairShop.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutoRepairShop.Api.Controllers
{
    [Route("api/service-order")]
    [ApiController]
    public class ServiceOrderController(IServiceOrderService service, ILogger<ServiceOrderController> logger) : ControllerBase
    {
        private readonly IServiceOrderService _service = service;
        private readonly ILogger<ServiceOrderController> _logger = logger;

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([FromBody] CreateServiceOrderRequest request)
        {
            try
            {
               _logger.LogInformation("Criando ordem de serviço");
                if (!TryGetCurrentUserId(out var currentUserId))
                    return Unauthorized(new { error = "Invalid authenticated user." });

                await _service.CreateServiceOrderAsync(request, currentUserId);
                _logger.LogInformation("Ordem de serviço criada com sucesso"); 
                return Ok();
            }
            catch (DomainException ex)
            {
                _logger.LogError(ex, "Falha ao criar ordem de serviço");
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetStatus(Guid id)
        {
            try
            {
                var response = await _service.GetByIdAsync(id);
                return Ok(response);
            }
            catch (DomainException ex)
            {
                return NotFound(new { error = ex.Message });
            }
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAll([FromQuery] ServiceOrderStatus? status = null)
        {
            var response = await _service.GetAllAsync(status);
            return Ok(response);
        }

        [HttpPut("{id}/advance")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AdvanceStatus(Guid id)
        {
            try
            {
                if (!TryGetCurrentUserId(out var currentUserId))
                    return Unauthorized(new { error = "Invalid authenticated user." });

                await _service.AdvanceStatusAsync(id, currentUserId);
                return Ok(new { message = "Service order status advanced successfully." });
            }
            catch (DomainException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateInDiagnosisAndAdvance(
            Guid id,
            [FromBody] UpdateServiceOrderInDiagnosisRequest request
        )
        {
            try
            {
                if (!TryGetCurrentUserId(out var currentUserId))
                    return Unauthorized(new { error = "Invalid authenticated user." });

                await _service.UpdateInDiagnosisAndAdvanceAsync(id, request, currentUserId);
                return Ok(new { message = "Service order updated and advanced successfully." });
            }
            catch (DomainException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPut("decision")]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> ProcessApprovalDecision(
            [FromBody] ApprovalDecisionRequest request
        )
        {
            try
            {
                if (!TryGetCurrentUserId(out var currentUserId))
                    return Unauthorized(new { error = "Invalid authenticated user." });

                await _service.ProcessApprovalDecisionAsync(request, currentUserId);

                var message = request.IsApproved
                    ? "Service order approved successfully."
                    : "Service order canceled successfully.";

                return Ok(new { message });
            }
            catch (DomainException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("metrics/average-execution-time")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAverageExecutionTime()
        {
            try
            {
                var response = await _service.GetAverageExecutionTimeAsync();
                return Ok(response);
            }
            catch (DomainException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        private bool TryGetCurrentUserId(out Guid userId)
        {
            userId = Guid.Empty;

            var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return value is not null && Guid.TryParse(value, out userId);
        }
    }
}
