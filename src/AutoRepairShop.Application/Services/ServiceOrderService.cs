using AutoMapper;
using AutoRepairShop.Application.DTOs.Email.Messages;
using AutoRepairShop.Application.DTOs.ServiceOrder;
using AutoRepairShop.Application.DTOs.ServiceOrder.Request;
using AutoRepairShop.Application.DTOs.ServiceOrder.Response;
using AutoRepairShop.Application.DTOs.Supply;
using AutoRepairShop.Application.Interfaces.Services;
using AutoRepairShop.Domain.Entities;
using AutoRepairShop.Domain.Entities.ServiceOrder;
using AutoRepairShop.Domain.Enums;
using AutoRepairShop.Domain.Exceptions;
using AutoRepairShop.Domain.Interfaces.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace AutoRepairShop.Application.Services
{
    public class ServiceOrderService(
        IServiceOrderRepository repository,
        ICustomerService customerService,
        IVehicleService vehicleService,
        IServiceService serviceService,
        ISupplyService supplyService,
        IEmailService emailService,
        IMapper mapper,
        ILogger<ServiceOrderService> logger
    ) : IServiceOrderService
    {
        private readonly IServiceOrderRepository _repository = repository;
        private readonly ICustomerService _customerService = customerService;
        private readonly IVehicleService _vehicleService = vehicleService;
        private readonly IServiceService _serviceService = serviceService;
        private readonly ISupplyService _supplyService = supplyService;
        private readonly IEmailService _emailService = emailService;
        private readonly IMapper _mapper = mapper;
        private readonly ILogger<ServiceOrderService> _logger = logger;

        public async Task CreateServiceOrderAsync(
            CreateServiceOrderRequest request,
            Guid createdById
        )
        {
            Customer customer = await _customerService.GetByCpfCnpjAsync(request.CustomerDocument);

            Vehicle vehicle = await _vehicleService.GetOrCreateAsync(request.Vehicle, customer.Id);

            List<Service> services = await _serviceService.GetServicesByIdsAsync(
                request.ServiceIds
            );

            List<Supply> supplies = await _supplyService.GetSuppliesInStockAsync(
                request.SupplyItems
            );

            if (services.IsNullOrEmpty() && supplies.IsNullOrEmpty())
                throw new DomainException(
                    "No services or supplies available for the service order."
                );

            var serviceOrder = new ServiceOrder
            {
                Id = Guid.NewGuid(),
                CustomerId = customer.Id,
                VehicleId = vehicle.Id,
            };

            services.ForEach(s => serviceOrder.AddService(s.Id));
            supplies.ForEach(s =>
                serviceOrder.AddSupply(
                    s.Id,
                    request.SupplyItems.First(i => i.SupplyId == s.Id).Quantity
                )
            );

            serviceOrder.AddHistory(serviceOrder.Status, createdById);

            await _repository.AddAsync(serviceOrder);
            await NotifyCustomerStatusChangedAsync(serviceOrder);
        }

        public async Task UpdateInDiagnosisAndAdvanceAsync(
            Guid serviceOrderId,
            UpdateServiceOrderInDiagnosisRequest request,
            Guid changedById
        )
        {
            var serviceOrder =
                await _repository.GetByIdAsync(serviceOrderId)
                ?? throw new DomainException("Service order not found.");

            if (serviceOrder.Status != ServiceOrderStatus.InDiagnosis)
                throw new DomainException(
                    "Only service orders in diagnosis can be edited by admin."
                );

            if (request.ServiceIds.IsNullOrEmpty() && request.SupplyItems.IsNullOrEmpty())
                throw new DomainException(
                    "No services or supplies available for the service order."
                );

            if (request.ServiceIds.GroupBy(x => x).Any(x => x.Count() > 1))
                throw new DomainException("Duplicate services are not allowed.");

            if (request.SupplyItems.GroupBy(x => x.SupplyId).Any(x => x.Count() > 1))
                throw new DomainException("Duplicate supplies are not allowed.");

            if (request.SupplyItems.Any(x => x.Quantity <= 0))
                throw new DomainException("Supply quantity must be greater than zero.");

            List<Service> services = await _serviceService.GetServicesByIdsAsync(
                request.ServiceIds
            );

            if (services.Count != request.ServiceIds.Count)
                throw new DomainException("One or more services were not found.");

            var currentSupplyQuantities = serviceOrder.Supplies.ToDictionary(
                x => x.SupplyId,
                x => x.Quantity
            );
            var requestedSupplyQuantities = request.SupplyItems.ToDictionary(
                x => x.SupplyId,
                x => x.Quantity
            );

            var suppliesToReserve = requestedSupplyQuantities
                .Where(x => x.Value > currentSupplyQuantities.GetValueOrDefault(x.Key, 0))
                .Select(x => new SupplyItemDto
                {
                    SupplyId = x.Key,
                    Quantity = x.Value - currentSupplyQuantities.GetValueOrDefault(x.Key, 0),
                })
                .ToList();

            List<Supply> reservedSupplies = await _supplyService.GetSuppliesInStockAsync(
                suppliesToReserve
            );

            if (reservedSupplies.Count != suppliesToReserve.Count)
                throw new DomainException("One or more supplies do not have enough stock.");

            var suppliesToRestock = currentSupplyQuantities
                .Where(x => requestedSupplyQuantities.GetValueOrDefault(x.Key, 0) < x.Value)
                .Select(x =>
                    (x.Key, x.Value - requestedSupplyQuantities.GetValueOrDefault(x.Key, 0))
                )
                .ToList();

            if (!suppliesToRestock.IsNullOrEmpty())
            {
                await _supplyService.RestockSuppliesAsync(suppliesToRestock);
            }

            serviceOrder.ReplaceServices(request.ServiceIds);
            serviceOrder.ReplaceSupplies([
                .. request.SupplyItems.Select(x => (x.SupplyId, x.Quantity)),
            ]);

            serviceOrder.RequestApproval();
            serviceOrder.AddHistory(serviceOrder.Status, changedById);
            await _repository.UpdateAsync(serviceOrder);
            await NotifyCustomerStatusChangedAsync(
                serviceOrder,
                includeSummary: serviceOrder.Status == ServiceOrderStatus.WaitingApproval
            );
        }

        public async Task<GetServiceOrderResponse> GetByIdAsync(Guid serviceOrderId)
        {
            var serviceOrder =
                await _repository.GetByIdAsync(serviceOrderId)
                ?? throw new DomainException("Service order not found.");

            return _mapper.Map<GetServiceOrderResponse>(serviceOrder);
        }

        public async Task<IEnumerable<GetServiceOrderResponse>> GetAllAsync(
            ServiceOrderStatus? status
        )
        {
            var serviceOrders = await _repository.GetAllAsync(status);
            return _mapper.Map<IEnumerable<GetServiceOrderResponse>>(serviceOrders);
        }

        public async Task AdvanceStatusAsync(Guid serviceOrderId, Guid changedById)
        {
            ServiceOrder serviceOrder =
                await _repository.GetByIdAsync(serviceOrderId)
                ?? throw new DomainException("Service order not found.");

            switch (serviceOrder.Status)
            {
                case ServiceOrderStatus.Received:
                    serviceOrder.StartDiagnosis();
                    break;
                case ServiceOrderStatus.InDiagnosis:
                    serviceOrder.RequestApproval();
                    break;
                case ServiceOrderStatus.InExecution:
                    serviceOrder.Finish();
                    break;
                case ServiceOrderStatus.Finished:
                    serviceOrder.Deliver();
                    break;
                case ServiceOrderStatus.WaitingApproval:
                    throw new DomainException(
                        "Only customer can decide while status is waiting approval."
                    );
                case ServiceOrderStatus.Canceled:
                    throw new DomainException("Canceled service order cannot be advanced.");
                case ServiceOrderStatus.Delivered:
                    throw new DomainException("Service order is already delivered.");
                default:
                    throw new DomainException("Invalid service order status transition.");
            }

            serviceOrder.AddHistory(serviceOrder.Status, changedById);

            await _repository.UpdateAsync(serviceOrder);
            await NotifyCustomerStatusChangedAsync(
                serviceOrder,
                includeSummary: serviceOrder.Status == ServiceOrderStatus.WaitingApproval
            );
        }

        public async Task ProcessApprovalDecisionAsync(
            ApprovalDecisionRequest request,
            Guid changedById
        )
        {
            var serviceOrder =
                await _repository.GetByIdAsync(request.ServiceOrderId)
                ?? throw new DomainException("Service order not found.");

            if (request.IsApproved)
            {
                serviceOrder.Approve();
            }
            else
            {
                serviceOrder.Reject();

                if (!serviceOrder.Supplies.IsNullOrEmpty())
                {
                    var suppliesToRestock = serviceOrder
                        .Supplies.Select(s => (s.SupplyId, s.Quantity))
                        .ToList();

                    await _supplyService.RestockSuppliesAsync(suppliesToRestock);
                }
            }

            serviceOrder.AddHistory(serviceOrder.Status, changedById);

            await _repository.UpdateAsync(serviceOrder);
            await NotifyCustomerStatusChangedAsync(serviceOrder);
        }

        public async Task<AverageExecutionTimeResponse> GetAverageExecutionTimeAsync()
        {
            var (total, completed, averageHours, earliest, latest) =
                await _repository.GetAverageExecutionTimeAsync();

            return new AverageExecutionTimeResponse
            {
                TotalServiceOrders = total,
                CompletedServiceOrders = completed,
                AverageExecutionTimeInHours = Math.Round(averageHours, 2),
                AverageExecutionTimeInDays = Math.Round(averageHours / 24, 2),
                EarliestStartDate = earliest,
                LatestFinishDate = latest,
            };
        }

        public async Task<AverageStatusDurationResponse> GetAverageStatusDurationsAsync()
        {
            var (averageInDiagnosis, averageInExecution, averageFinished) =
                await _repository.GetAverageStatusDurationsAsync();

            return new AverageStatusDurationResponse
            {
                AverageInDiagnosisDuration = averageInDiagnosis,
                AverageInExecutionDuration = averageInExecution,
                AverageFinishedDuration = averageFinished,
            };
        }

        private async Task NotifyCustomerStatusChangedAsync(
            ServiceOrder serviceOrder,
            bool includeSummary = false
        )
        {
            try
            {
                var customer = await _customerService.GetEntityByIdAsync(serviceOrder.CustomerId);

                ServiceOrderSummary? summary = includeSummary
                    ? await BuildServiceOrderSummaryAsync(serviceOrder)
                    : null;

                var message = new ServiceOrderStatusChangedEmailMessage(
                    customer,
                    serviceOrder,
                    summary
                );

                await _emailService.SendAsync(message);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to send status change email for service order {ServiceOrderId}",
                    serviceOrder.Id
                );
            }
        }

        private async Task<ServiceOrderSummary> BuildServiceOrderSummaryAsync(
            ServiceOrder serviceOrder
        )
        {
            var summary = new ServiceOrderSummary { ServiceOrderId = serviceOrder.Id };

            // Get services details
            if (!serviceOrder.Services.IsNullOrEmpty())
            {
                var serviceIds = serviceOrder.Services.Select(s => s.ServiceId).ToList();
                var services = await _serviceService.GetServicesByIdsAsync(serviceIds);
                summary.Services =
                [
                    .. services.Select(s => new ServiceSummary
                    {
                        ServiceId = s.Id,
                        Name = s.Name,
                        Price = s.Price,
                    }),
                ];
            }

            // Get supplies details
            if (!serviceOrder.Supplies.IsNullOrEmpty())
            {
                var supplyIds = serviceOrder.Supplies.Select(s => s.SupplyId).ToList();
                var supplies = await _supplyService.GetSuppliesInStockAsync([
                    .. supplyIds.Select(id => new SupplyItemDto { SupplyId = id, Quantity = 1 }),
                ]);

                var suppliesDict = supplies.ToDictionary(s => s.Id);
                summary.Supplies =
                [
                    .. serviceOrder
                        .Supplies.Where(s => suppliesDict.ContainsKey(s.SupplyId))
                        .Select(s => new SupplySummary
                        {
                            SupplyId = s.SupplyId,
                            Name = suppliesDict[s.SupplyId].Name,
                            Price = suppliesDict[s.SupplyId].Price,
                            Quantity = s.Quantity,
                        }),
                ];
            }

            // Calculate total
            decimal servicesTotal = summary.Services.Sum(s => s.Price);
            decimal suppliesTotal = summary.Supplies.Sum(s => s.Price * s.Quantity);
            summary.TotalAmount = servicesTotal + suppliesTotal;

            return summary;
        }
    }
}
