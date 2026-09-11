using System.Security.Claims;
using AutoRepairShop.Api.Controllers;
using AutoRepairShop.Application.DTOs.ServiceOrder.Request;
using AutoRepairShop.Application.DTOs.ServiceOrder.Response;
using AutoRepairShop.Application.DTOs.Supply;
using AutoRepairShop.Application.DTOs.Vehicle;
using AutoRepairShop.Application.Interfaces.Services;
using AutoRepairShop.Domain.Enums;
using AutoRepairShop.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace AutoRepairShop.Tests.Controllers;

public class ServiceOrderControllerTests
{
    private readonly Mock<IServiceOrderService> _serviceMock = new();
    private readonly Mock<ILogger<ServiceOrderController>> _loggerMock = new();

    [Fact]
    public async Task Create_WhenUserIdIsMissing_ShouldReturnUnauthorized()
    {
        var controller = CreateController();
        var result = await controller.Create(CreateRequest());
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(
            "Invalid authenticated user.",
            ControllerResultValueReader.GetString(unauthorizedResult.Value, "error")
        );
    }

    [Fact]
    public async Task Create_WhenRequestIsValid_ShouldReturnOk()
    {
        var userId = Guid.NewGuid();
        var request = CreateRequest();
        var controller = CreateController(userId);
        var result = await controller.Create(request);
        Assert.IsType<OkResult>(result);
        _serviceMock.Verify(
            service => service.CreateServiceOrderAsync(request, userId),
            Times.Once
        );
    }

    [Fact]
    public async Task Create_WhenDomainExceptionIsThrown_ShouldReturnBadRequest()
    {
        var userId = Guid.NewGuid();
        var request = CreateRequest();
        _serviceMock
            .Setup(service => service.CreateServiceOrderAsync(request, userId))
            .ThrowsAsync(new DomainException("invalid service order"));
        var controller = CreateController(userId);
        var result = await controller.Create(request);
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(
            "invalid service order",
            ControllerResultValueReader.GetString(badRequestResult.Value, "error")
        );
    }

    [Fact]
    public async Task GetStatus_WhenServiceOrderExists_ShouldReturnOkWithPayload()
    {
        var serviceOrderId = Guid.NewGuid();
        var response = new GetServiceOrderResponse
        {
            Id = serviceOrderId,
            Status = ServiceOrderStatus.Received,
        };
        _serviceMock.Setup(service => service.GetByIdAsync(serviceOrderId)).ReturnsAsync(response);
        var controller = CreateController();
        var result = await controller.GetStatus(serviceOrderId);
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Same(response, okResult.Value);
    }

    [Fact]
    public async Task GetStatus_WhenDomainExceptionIsThrown_ShouldReturnNotFound()
    {
        var serviceOrderId = Guid.NewGuid();
        _serviceMock
            .Setup(service => service.GetByIdAsync(serviceOrderId))
            .ThrowsAsync(new DomainException("service order not found"));
        var controller = CreateController();
        var result = await controller.GetStatus(serviceOrderId);
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(
            "service order not found",
            ControllerResultValueReader.GetString(notFoundResult.Value, "error")
        );
    }

    [Fact]
    public async Task GetAll_WhenFilterIsProvided_ShouldReturnOkWithPayload()
    {
        IEnumerable<GetServiceOrderResponse> response =
        [
            new GetServiceOrderResponse
            {
                Id = Guid.NewGuid(),
                Status = ServiceOrderStatus.Received,
            },
        ];
        _serviceMock
            .Setup(service => service.GetAllAsync(ServiceOrderStatus.Received))
            .ReturnsAsync(response);
        var controller = CreateController();
        var result = await controller.GetAll(ServiceOrderStatus.Received);
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Same(response, okResult.Value);
    }

    [Fact]
    public async Task AdvanceStatus_WhenUserIdIsMissing_ShouldReturnUnauthorized()
    {
        var controller = CreateController();
        var result = await controller.AdvanceStatus(Guid.NewGuid());
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(
            "Invalid authenticated user.",
            ControllerResultValueReader.GetString(unauthorizedResult.Value, "error")
        );
    }

    [Fact]
    public async Task AdvanceStatus_WhenRequestIsValid_ShouldReturnOkWithMessage()
    {
        var userId = Guid.NewGuid();
        var serviceOrderId = Guid.NewGuid();
        var controller = CreateController(userId);
        var result = await controller.AdvanceStatus(serviceOrderId);
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(
            "Service order status advanced successfully.",
            ControllerResultValueReader.GetString(okResult.Value, "message")
        );
        _serviceMock.Verify(
            service => service.AdvanceStatusAsync(serviceOrderId, userId),
            Times.Once
        );
    }

    [Fact]
    public async Task AdvanceStatus_WhenDomainExceptionIsThrown_ShouldReturnBadRequest()
    {
        var userId = Guid.NewGuid();
        var serviceOrderId = Guid.NewGuid();
        _serviceMock
            .Setup(service => service.AdvanceStatusAsync(serviceOrderId, userId))
            .ThrowsAsync(new DomainException("invalid transition"));
        var controller = CreateController(userId);
        var result = await controller.AdvanceStatus(serviceOrderId);
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(
            "invalid transition",
            ControllerResultValueReader.GetString(badRequestResult.Value, "error")
        );
    }

    [Fact]
    public async Task UpdateInDiagnosisAndAdvance_WhenUserIdIsMissing_ShouldReturnUnauthorized()
    {
        var controller = CreateController();
        var request = new UpdateServiceOrderInDiagnosisRequest
        {
            ServiceIds = [Guid.NewGuid()],
            SupplyItems = [],
        };
        var result = await controller.UpdateInDiagnosisAndAdvance(Guid.NewGuid(), request);
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(
            "Invalid authenticated user.",
            ControllerResultValueReader.GetString(unauthorizedResult.Value, "error")
        );
    }

    [Fact]
    public async Task UpdateInDiagnosisAndAdvance_WhenRequestIsValid_ShouldReturnOkWithMessage()
    {
        var userId = Guid.NewGuid();
        var serviceOrderId = Guid.NewGuid();
        var request = new UpdateServiceOrderInDiagnosisRequest
        {
            ServiceIds = [Guid.NewGuid()],
            SupplyItems = [new SupplyItemDto { SupplyId = Guid.NewGuid(), Quantity = 2 }],
        };
        var controller = CreateController(userId);
        var result = await controller.UpdateInDiagnosisAndAdvance(serviceOrderId, request);
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(
            "Service order updated and advanced successfully.",
            ControllerResultValueReader.GetString(okResult.Value, "message")
        );
        _serviceMock.Verify(
            service => service.UpdateInDiagnosisAndAdvanceAsync(serviceOrderId, request, userId),
            Times.Once
        );
    }

    [Fact]
    public async Task UpdateInDiagnosisAndAdvance_WhenDomainExceptionIsThrown_ShouldReturnBadRequest()
    {
        var userId = Guid.NewGuid();
        var serviceOrderId = Guid.NewGuid();
        var request = new UpdateServiceOrderInDiagnosisRequest
        {
            ServiceIds = [Guid.NewGuid()],
            SupplyItems = [],
        };
        _serviceMock
            .Setup(service =>
                service.UpdateInDiagnosisAndAdvanceAsync(serviceOrderId, request, userId)
            )
            .ThrowsAsync(new DomainException("order not in diagnosis"));
        var controller = CreateController(userId);
        var result = await controller.UpdateInDiagnosisAndAdvance(serviceOrderId, request);
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(
            "order not in diagnosis",
            ControllerResultValueReader.GetString(badRequestResult.Value, "error")
        );
    }

    [Theory]
    [InlineData(true, "Service order approved successfully.")]
    [InlineData(false, "Service order canceled successfully.")]
    public async Task ProcessApprovalDecision_WhenRequestIsValid_ShouldReturnOkWithExpectedMessage(
        bool isApproved,
        string expectedMessage
    )
    {
        var userId = Guid.NewGuid();
        var request = new ApprovalDecisionRequest
        {
            ServiceOrderId = Guid.NewGuid(),
            IsApproved = isApproved,
        };
        var controller = CreateController(userId);
        var result = await controller.ProcessApprovalDecision(request);
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(
            expectedMessage,
            ControllerResultValueReader.GetString(okResult.Value, "message")
        );
        _serviceMock.Verify(
            service => service.ProcessApprovalDecisionAsync(request, userId),
            Times.Once
        );
    }

    [Fact]
    public async Task ProcessApprovalDecision_WhenUserIdIsMissing_ShouldReturnUnauthorized()
    {
        var controller = CreateController();
        var result = await controller.ProcessApprovalDecision(
            new ApprovalDecisionRequest { ServiceOrderId = Guid.NewGuid(), IsApproved = true }
        );
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(
            "Invalid authenticated user.",
            ControllerResultValueReader.GetString(unauthorizedResult.Value, "error")
        );
    }

    [Fact]
    public async Task ProcessApprovalDecision_WhenDomainExceptionIsThrown_ShouldReturnBadRequest()
    {
        var userId = Guid.NewGuid();
        var request = new ApprovalDecisionRequest
        {
            ServiceOrderId = Guid.NewGuid(),
            IsApproved = true,
        };
        _serviceMock
            .Setup(service => service.ProcessApprovalDecisionAsync(request, userId))
            .ThrowsAsync(new DomainException("approval failed"));
        var controller = CreateController(userId);
        var result = await controller.ProcessApprovalDecision(request);
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(
            "approval failed",
            ControllerResultValueReader.GetString(badRequestResult.Value, "error")
        );
    }

    [Fact]
    public async Task GetAverageExecutionTime_WhenServiceSucceeds_ShouldReturnOkWithPayload()
    {
        var response = new AverageExecutionTimeResponse
        {
            TotalServiceOrders = 10,
            CompletedServiceOrders = 8,
            AverageExecutionTimeInHours = 24,
            AverageExecutionTimeInDays = 1,
        };
        _serviceMock
            .Setup(service => service.GetAverageExecutionTimeAsync())
            .ReturnsAsync(response);
        var controller = CreateController();
        var result = await controller.GetAverageExecutionTime();
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Same(response, okResult.Value);
    }

    [Fact]
    public async Task GetAverageExecutionTime_WhenDomainExceptionIsThrown_ShouldReturnBadRequest()
    {
        _serviceMock
            .Setup(service => service.GetAverageExecutionTimeAsync())
            .ThrowsAsync(new DomainException("metrics unavailable"));
        var controller = CreateController();
        var result = await controller.GetAverageExecutionTime();
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(
            "metrics unavailable",
            ControllerResultValueReader.GetString(badRequestResult.Value, "error")
        );
    }

    private ServiceOrderController CreateController(Guid? userId = null)
    {
        // Única alteração: adicionado _loggerMock.Object como segundo parâmetro
        var controller = new ServiceOrderController(_serviceMock.Object, _loggerMock.Object);
        var claims = new List<Claim>();
        if (userId.HasValue)
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(
                    new ClaimsIdentity(claims, userId.HasValue ? "TestAuth" : null)
                ),
            },
        };
        return controller;
    }

    private static CreateServiceOrderRequest CreateRequest()
    {
        return new CreateServiceOrderRequest
        {
            CustomerDocument = "12345678900",
            Vehicle = new VehicleDto
            {
                Plate = "ABC1234",
                Brand = "Ford",
                Model = "Ka",
                Year = 2022,
            },
            ServiceIds = [Guid.NewGuid()],
            SupplyItems = [new SupplyItemDto { SupplyId = Guid.NewGuid(), Quantity = 1 }],
        };
    }
}
