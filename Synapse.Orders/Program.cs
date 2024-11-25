using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Synapse.Orders
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var serviceProvider = new ServiceCollection()
                .AddLogging(configure => configure.AddConsole())
                .AddSingleton<IOrderService, OrderService>()
                .AddSingleton<OrderProcessor>()
                .AddHttpClient()
                .AddSingleton<IConfiguration>(provider =>
                {
                    var configuration = new ConfigurationBuilder()
                        .AddJsonFile("appsettings.json")
                        .Build();
                    return configuration;
                })
                .BuildServiceProvider();

            var orderProcessor = serviceProvider.GetService<OrderProcessor>();

            if (orderProcessor == null)
            {
                var logger = serviceProvider.GetService<ILogger<Program>>();
                logger?.LogError("OrderProcessor is not available.");
                Environment.Exit(1);
            }
            else
            {
                await orderProcessor.ProcessOrdersAsync();
            }
        }
    }
}