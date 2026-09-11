using StatsdClient;

namespace AutoRepairShop.Api.Configuration;

public static class DogStatsdConfiguration
{
    public static IServiceCollection AddDogStatsdMetrics(this IServiceCollection services)
    {
        services.AddSingleton<DogStatsdService>(sp =>
        {
            var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("DogStatsd");
            var dogStatsd = new DogStatsdService();
            var config = BuildConfig();

            Exception? configureError = null;
            var ok = dogStatsd.Configure(config, ex => configureError = ex);

            if (!ok)
            {
                logger.LogError(
                    configureError,
                    "DogStatsD configure failed. Server={Server} Port={Port}",
                    config.StatsdServerName,
                    config.StatsdPort
                );
            }
            else
            {
                logger.LogInformation(
                    "DogStatsD configured. Server={Server} Port={Port}",
                    config.StatsdServerName,
                    config.StatsdPort
                );
            }

            return dogStatsd;
        });

        services.AddSingleton<IDogStatsd>(sp => sp.GetRequiredService<DogStatsdService>());
        return services;
    }

    private static StatsdConfig BuildConfig()
    {
        // Valores fixos para teste (baseados no deployment/pod do EKS).
        // DD_AGENT_HOST é dinâmico (status.hostIP) — com UDS não é necessário.
        const string dogStatsdUrl = "unix:///var/run/datadog/dsd.socket";
        const string env = "production";
        const int dogStatsdPort = 8125;

        return new StatsdConfig
        {
            StatsdServerName = dogStatsdUrl,
            StatsdPort = dogStatsdPort,
            ConstantTags =
            [
                $"env:{env}",
                "service:autorepairshop-api",
            ],
        };
    }
}
