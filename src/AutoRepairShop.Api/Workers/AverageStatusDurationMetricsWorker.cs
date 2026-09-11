using AutoRepairShop.Application.Interfaces.Services;
using StatsdClient;

namespace AutoRepairShop.Api.Workers;

public class AverageStatusDurationMetricsWorker(
    IServiceScopeFactory scopeFactory,
    IDogStatsd dogStatsd,
    ILogger<AverageStatusDurationMetricsWorker> logger
) : BackgroundService
{
    private static readonly TimeSpan PublishInterval = TimeSpan.FromMinutes(1);

    private const string DiagnosisMetric = "autorepair.service_order.avg_duration.diagnosis";
    private const string ExecutionMetric = "autorepair.service_order.avg_duration.execution";
    private const string FinishedMetric = "autorepair.service_order.avg_duration.finished";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Starting average status duration metrics publisher (interval: {Interval})",
            PublishInterval
        );

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PublishAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to publish average status duration metrics to Datadog");
            }

            try
            {
                await Task.Delay(PublishInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task PublishAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var serviceOrderService = scope.ServiceProvider.GetRequiredService<IServiceOrderService>();
        var metrics = await serviceOrderService.GetAverageStatusDurationsAsync();

        cancellationToken.ThrowIfCancellationRequested();

        dogStatsd.Gauge(DiagnosisMetric, metrics.AverageInDiagnosisDuration.TotalSeconds);
        dogStatsd.Gauge(ExecutionMetric, metrics.AverageInExecutionDuration.TotalSeconds);
        dogStatsd.Gauge(FinishedMetric, metrics.AverageFinishedDuration.TotalSeconds);
        dogStatsd.Flush();

        logger.LogInformation(
            "Published status duration metrics. Diagnosis={Diagnosis}s Execution={Execution}s Finished={Finished}s",
            metrics.AverageInDiagnosisDuration.TotalSeconds,
            metrics.AverageInExecutionDuration.TotalSeconds,
            metrics.AverageFinishedDuration.TotalSeconds
        );
    }
}
