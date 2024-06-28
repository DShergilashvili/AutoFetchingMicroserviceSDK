using Microsoft.Extensions.DependencyInjection;

namespace AutoFetchingMicroserviceSDK
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddAutoFetchingSDK(this IServiceCollection services, Action<AutoFetchingOptions> configureOptions)
        {
            services.AddOptions<AutoFetchingOptions>().Configure(configureOptions);
            services.AddHttpClient();
            services.AddScoped<IAutoFetchingService, AutoFetchingService>();

            return services;
        }
    }
}
