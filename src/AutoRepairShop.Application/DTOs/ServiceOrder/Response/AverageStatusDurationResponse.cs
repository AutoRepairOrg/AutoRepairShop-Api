namespace AutoRepairShop.Application.DTOs.ServiceOrder.Response
{
    public class AverageStatusDurationResponse
    {
        public TimeSpan AverageInDiagnosisDuration { get; set; }
        public TimeSpan AverageInExecutionDuration { get; set; }
        public TimeSpan AverageFinishedDuration { get; set; }
    }
}
