using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System.Text;
using Newtonsoft.Json;
using Synapse.Orders.Models;

namespace Synapse.Orders
{
    /// <summary>
    /// Service for handling orders.
    /// </summary>
    public class OrderService : IOrderService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<OrderService> _logger;
        private readonly IConfiguration _configuration;

        private readonly string _ordersApiUrl;
        private readonly string _alertApiUrl;
        private readonly string _updateApiUrl;
        

        public OrderService(IHttpClientFactory httpClientFactory, ILogger<OrderService> logger,
            IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _configuration = configuration;

            _ordersApiUrl = $"{_configuration["ApiSettings:OrdersApiUrl"]}/orders";
            _alertApiUrl = $"{_configuration["ApiSettings:AlertApiUrl"]}/alerts";
            _updateApiUrl = $"{_configuration["ApiSettings:UpdateApiUrl"]}/update";
            
            if (string.IsNullOrEmpty(_ordersApiUrl) || string.IsNullOrEmpty(_alertApiUrl) || string.IsNullOrEmpty(_updateApiUrl))
            {
                throw new InvalidOperationException("API URLs must be configured in appsettings.json");
            }
        }

        /// <summary>
        /// Fetches medical equipment orders from the API.
        /// </summary>
        /// <returns>An array of orders.</returns>
        public async Task<Order[]> FetchMedicalEquipmentOrders()
        {
            var orders = new List<Order>();
            var httpClient = _httpClientFactory.CreateClient();
            var nextPageUrl = _ordersApiUrl;

            while (!string.IsNullOrEmpty(nextPageUrl))
            {
                try
                {
                    var response = await httpClient.GetAsync(nextPageUrl);
                    response.EnsureSuccessStatusCode();

                    var json = await response.Content.ReadAsStringAsync();
                    var jsonObject = JObject.Parse(json);
                    var ordersArray = jsonObject["orders"]?.ToObject<Order[]>() ?? Array.Empty<Order>();
                    orders.AddRange(ordersArray);

                    nextPageUrl = jsonObject["nextPageUrl"]?.ToString();
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogError(ex, "Failed to fetch orders from API.");
                    throw; // Rethrow the exception to stop processing
                }
            }

            return orders.ToArray();
        }

        /// <summary>
        /// Processes the given order.
        /// </summary>
        /// <param name="order">The order to process.</param>
        /// <returns>The processed order.</returns>
        public async Task<Order> ProcessOrder(Order order)
        {
            try
            {
                if (order.Items == null)
                {
                    _logger.LogInformation("Order {OrderId} does not contain any items.", order.OrderId);
                    return order;
                }

                foreach (var item in order.Items)
                {
                    if (!IsItemDelivered(item))
                    {
                        continue;
                    }

                    await SendAlertMessage(item, order.OrderId);
                    IncrementDeliveryNotification(item);
                }

                return order;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while processing the order {OrderId}.", order.OrderId);
                throw;
            }
        }

        /// <summary>
        /// Determines whether the specified item is delivered.
        /// </summary>
        /// <param name="item">The item to check.</param>
        /// <returns><c>true</c> if the item is delivered; otherwise, <c>false</c>.</returns>
        public static bool IsItemDelivered(Item item)
        {
            return item.Status == ItemStatus.Delivered;
        }

        /// <summary>
        /// Sends an alert message for the delivered item.
        /// </summary>
        /// <param name="item">The delivered item.</param>
        /// <param name="orderId">The order ID.</param>
        private async Task SendAlertMessage(Item item, string orderId)
        {
            var alertData = new
            {
                Message = $"Alert for delivered item: Order {orderId}, Item: {item.Description}, " +
                          $"Delivery Notifications: {item.DeliveryNotification}"
            };
            var content = new StringContent(JsonConvert.SerializeObject(alertData), Encoding.UTF8, "application/json");

            try
            {
                var httpClient = _httpClientFactory.CreateClient();
                var response = await httpClient.PostAsync(_alertApiUrl, content);
                response.EnsureSuccessStatusCode();

                _logger.LogInformation("Alert sent for delivered item: {Description}", item.Description);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Failed to send alert for delivered item: {Description}", item.Description);
            }
        }

        /// <summary>
        /// Increments the delivery notification count for the item.
        /// </summary>
        /// <param name="item">The item to update.</param>
        private static void IncrementDeliveryNotification(Item item)
        {
            item.DeliveryNotification++;
        }

        /// <summary>
        /// Sends an alert and updates the order.
        /// </summary>
        /// <param name="order">The order to update.</param>
        public async Task SendAlertAndUpdateOrder(Order order)
        {
            var content = new StringContent(JsonConvert.SerializeObject(order), Encoding.UTF8, "application/json");

            try
            {
                var httpClient = _httpClientFactory.CreateClient();
                var response = await httpClient.PostAsync(_updateApiUrl, content);
                response.EnsureSuccessStatusCode();

                _logger.LogInformation("Updated order sent for processing: {OrderId}", order.OrderId);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Failed to send updated order for processing: {OrderId}", order.OrderId);
            }
        }
    }
}