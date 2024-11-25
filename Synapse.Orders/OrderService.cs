using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System.Text;
using Newtonsoft.Json;

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
        public async Task<JObject[]> FetchMedicalEquipmentOrders()
        {
            var orders = new List<JObject>();
            var httpClient = _httpClientFactory.CreateClient();
            var nextPageUrl = _ordersApiUrl;

            while (!string.IsNullOrEmpty(nextPageUrl))
            {
                try
                {
                    var response = await httpClient.GetAsync(nextPageUrl);
                    response.EnsureSuccessStatusCode();

                    await using (var stream = await response.Content.ReadAsStreamAsync())
                    using (var reader = new StreamReader(stream))
                    await using (var jsonReader = new JsonTextReader(reader))
                    {
                        var json = await JToken.ReadFromAsync(jsonReader);
                        var ordersArray = json["orders"]?.ToObject<JObject[]>() ?? Array.Empty<JObject>();
                        orders.AddRange(ordersArray);

                        nextPageUrl = json["nextPageUrl"]?.ToString();
                    }
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogError(ex, "Failed to fetch orders from API.");
                    break;
                }
            }

            return orders.ToArray();
        }

        /// <summary>
        /// Processes the given order.
        /// </summary>
        /// <param name="order">The order to process.</param>
        /// <returns>The processed order.</returns>
        public async Task<JObject> ProcessOrder(JObject order)
        {
            try
            {
                var items = order["Items"]?.ToObject<JArray>();
                if (items == null)
                {
                    _logger.LogInformation("Order {OrderId} does not contain any items.", order["OrderId"]);
                    return order;
                }

                foreach (var item in items)
                {
                    if (!IsItemDelivered(item))
                    {
                        continue;
                    }

                    var orderId = order["OrderId"]?.ToString();
                    if (orderId == null)
                    {
                        continue;
                    }

                    await SendAlertMessage(item, orderId);
                    IncrementDeliveryNotification(item);
                }

                // Update the order with the modified items
                order["Items"] = items;

                return order;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while processing the order {OrderId}.", order["OrderId"]);
                throw;
            }
        }

        /// <summary>
        /// Determines whether the specified item is delivered.
        /// </summary>
        /// <param name="item">The item to check.</param>
        /// <returns><c>true</c> if the item is delivered; otherwise, <c>false</c>.</returns>
        public static bool IsItemDelivered(JToken item)
        {
            return item["Status"]?.ToString().Equals("Delivered", StringComparison.OrdinalIgnoreCase) ?? false;
        }

        /// <summary>
        /// Sends an alert message for the delivered item.
        /// </summary>
        /// <param name="item">The delivered item.</param>
        /// <param name="orderId">The order ID.</param>
        private async Task SendAlertMessage(JToken item, string orderId)
        {
            var alertData = new
            {
                Message = $"Alert for delivered item: Order {orderId}, Item: {item["Description"]}, " +
                          $"Delivery Notifications: {item["deliveryNotification"]}"
            };
            var content = new StringContent(JObject.FromObject(alertData).ToString(), Encoding.UTF8,
                "application/json");

            try
            {
                var httpClient = _httpClientFactory.CreateClient();
                var response = await httpClient.PostAsync(_alertApiUrl, content);
                response.EnsureSuccessStatusCode();

                _logger.LogInformation("Alert sent for delivered item: {Description}", item["Description"]);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Failed to send alert for delivered item: {Description}", item["Description"]);
            }
        }

        /// <summary>
        /// Increments the delivery notification count for the item.
        /// </summary>
        /// <param name="item">The item to update.</param>
        private static void IncrementDeliveryNotification(JToken item)
        {
            item["deliveryNotification"] = (item["deliveryNotification"]?.Value<int>() ?? 0) + 1;
        }

        /// <summary>
        /// Sends an alert and updates the order.
        /// </summary>
        /// <param name="order">The order to update.</param>
        public async Task SendAlertAndUpdateOrder(JObject order)
        {
            var content = new StringContent(order.ToString(), Encoding.UTF8, "application/json");

            try
            {
                var httpClient = _httpClientFactory.CreateClient();
                var response = await httpClient.PostAsync(_updateApiUrl, content);
                response.EnsureSuccessStatusCode();

                _logger.LogInformation("Updated order sent for processing: {OrderId}", order["OrderId"]);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Failed to send updated order for processing: {OrderId}", order["OrderId"]);
            }
        }
    }
}