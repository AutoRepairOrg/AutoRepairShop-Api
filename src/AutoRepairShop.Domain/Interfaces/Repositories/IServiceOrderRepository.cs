using AutoRepairShop.Domain.Entities.ServiceOrder;
using AutoRepairShop.Domain.Enums;

namespace AutoRepairShop.Domain.Interfaces.Repositories
{
    public interface IServiceOrderRepository
    {
        Task AddAsync(ServiceOrder serviceOrder);
        Task<ServiceOrder?> GetByIdAsync(Guid id);
        Task<List<ServiceOrder>> GetAllAsync(ServiceOrderStatus? status);
        Task UpdateAsync(ServiceOrder serviceOrder);
        Task<(
            int total,
            int completed,
            double averageHours,
            DateTime? earliest,
            DateTime? latest
        )> GetAverageExecutionTimeAsync();

        Task<(
            TimeSpan averageInDiagnosis,
            TimeSpan averageInExecution,
            TimeSpan averageFinished
        )> GetAverageStatusDurationsAsync();
    }
}
