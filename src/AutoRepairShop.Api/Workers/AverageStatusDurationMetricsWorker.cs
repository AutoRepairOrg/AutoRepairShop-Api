using AutoRepairShop.Application.Interfaces.Services;
using StatsdClient;

namespace AutoRepairShop.Api.Workers;

public class AverageStatusDurationMetricsWorker(
    IServiceScopeFactory scopeFactory,
    IDogStatsd dogStatsd,
    ILogger<AverageStatusDurationMetricsWorker> logger
) : BackgroundService
{
    private static readonly TimeSpan PublishInterval = TimeSpan.FromSeconds(60);

    private const string MetricName = "service_order.avg_duration_seconds";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Starting service order status duration metrics publisher (interval: {Interval})",
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
                logger.LogWarning(ex, "Failed to publish service_order.avg_duration_seconds to Datadog");
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

        dogStatsd.Gauge(
            MetricName,
            metrics.AverageInDiagnosisDuration.TotalSeconds,
            tags: ["status:in_diagnosis"]
        );
        dogStatsd.Gauge(
            MetricName,
            metrics.AverageInExecutionDuration.TotalSeconds,
            tags: ["status:in_execution"]
        );
        dogStatsd.Gauge(
            MetricName,
            metrics.AverageFinishedDuration.TotalSeconds,
            tags: ["status:finished"]
        );
        dogStatsd.Flush();

        logger.LogInformation(
            "Published {Metric}. in_diagnosis={Diagnosis}s in_execution={Execution}s finished={Finished}s",
            MetricName,
            metrics.AverageInDiagnosisDuration.TotalSeconds,
            metrics.AverageInExecutionDuration.TotalSeconds,
            metrics.AverageFinishedDuration.TotalSeconds
        );
    }
}
