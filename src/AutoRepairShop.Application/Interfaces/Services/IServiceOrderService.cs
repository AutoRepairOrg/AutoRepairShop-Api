using AutoRepairShop.Application.DTOs.ServiceOrder.Request;
using AutoRepairShop.Application.DTOs.ServiceOrder.Response;
using AutoRepairShop.Domain.Enums;

namespace AutoRepairShop.Application.Interfaces.Services;

public interface IServiceOrderService
{
    Task CreateServiceOrderAsync(CreateServiceOrderRequest request, Guid createdById);

    Task UpdateInDiagnosisAndAdvanceAsync(
        Guid serviceOrderId,
        UpdateServiceOrderInDiagnosisRequest request,
        Guid changedById
    );

    Task<IEnumerable<GetServiceOrderResponse>> GetAllAsync(ServiceOrderStatus? status);

    Task<GetServiceOrderResponse> GetByIdAsync(Guid serviceOrderId);

    Task AdvanceStatusAsync(Guid serviceOrderId, Guid changedById);

    Task ProcessApprovalDecisionAsync(ApprovalDecisionRequest request, Guid changedById);

    Task<AverageExecutionTimeResponse> GetAverageExecutionTimeAsync();

    Task<AverageStatusDurationResponse> GetAverageStatusDurationsAsync();
}
