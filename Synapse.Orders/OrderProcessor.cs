using Microsoft.Extensions.Logging;

namespace Synapse.Orders
{
    /// <summary>
    /// Processor for handling order processing tasks.
    /// </summary>
    public class OrderProcessor
    {
        private readonly IOrderService _orderService;
        private readonly ILogger<OrderProcessor> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="OrderProcessor"/> class.
        /// </summary>
        public OrderProcessor(IOrderService orderService, ILogger<OrderProcessor> logger)
        {
            _orderService = orderService;
            _logger = logger;
        }

        /// <summary>
        /// Processes the orders asynchronously.
        /// </summary>
        public async Task ProcessOrdersAsync()
        {
            try
            {
                var medicalEquipmentOrders = await _orderService.FetchMedicalEquipmentOrders();
                foreach (var order in medicalEquipmentOrders)
                {
                    var updatedOrder = await _orderService.ProcessOrder(order);
                    await _orderService.SendAlertAndUpdateOrder(updatedOrder);
                }

                _logger.LogInformation("Results sent to relevant APIs.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while processing orders.");
            }
        }
    }
}