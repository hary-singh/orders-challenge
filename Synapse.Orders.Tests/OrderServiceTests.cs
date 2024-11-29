using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Synapse.Orders.Models;

namespace Synapse.Orders.Tests
{
    public class OrderServiceTests
    {
        private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
        private readonly Mock<ILogger<OrderService>> _loggerMock;
        private readonly Mock<IConfiguration> _configurationMock;
        private readonly OrderService _orderService;

        public OrderServiceTests()
        {
            _httpClientFactoryMock = new Mock<IHttpClientFactory>();
            _loggerMock = new Mock<ILogger<OrderService>>();
            _configurationMock = new Mock<IConfiguration>();
            _orderService = new OrderService(_httpClientFactoryMock.Object, _loggerMock.Object, _configurationMock.Object);
        }

        private void SetupHttpClient(HttpStatusCode statusCode, string content)
        {
            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = statusCode,
                    Content = new StringContent(content)
                });

            var httpClient = new HttpClient(handlerMock.Object)
            {
                BaseAddress = new Uri("https://api.example.com")
            };
            _httpClientFactoryMock.Setup(_ => _.CreateClient(It.IsAny<string>())).Returns(httpClient);
        }

        [Theory]
        [InlineData(ItemStatus.Delivered, true)]
        [InlineData(ItemStatus.Pending, false)]
        public void Test_IsItemDelivered(ItemStatus status, bool expected)
        {
            var item = new Item { Status = status, Description = "Test Item" };
            var result = OrderService.IsItemDelivered(item);
            Assert.Equal(expected, result);
        }

        [Fact]
        public async Task Test_FetchMedicalEquipmentOrders_ReturnsOrders_WhenApiCallIsSuccessful()
        {
            SetupHttpClient(HttpStatusCode.OK, "{\"orders\": [{\"OrderId\": \"1\", \"Items\": []}], \"nextPageUrl\": null}");

            var result = await _orderService.FetchMedicalEquipmentOrders();

            Assert.Single(result);
            Assert.Equal("1", result[0].OrderId);
        }

        [Fact]
        public async Task Test_FetchMedicalEquipmentOrders_ReturnsEmptyArray_WhenApiCallFails()
        {
            SetupHttpClient(HttpStatusCode.BadRequest, "");

            await Assert.ThrowsAsync<HttpRequestException>(async () => 
                await _orderService.FetchMedicalEquipmentOrders());
        }

        [Fact]
        public async Task Test_ProcessOrder_ProcessesDeliveredItems()
        {
            var order = new Order
            {
                OrderId = "1",
                Items = new List<Item>
                {
                    new Item
                    {
                        Status = ItemStatus.Delivered,
                        Description = "Item1",
                        DeliveryNotification = 0
                    }
                }
            };

            SetupHttpClient(HttpStatusCode.OK, "");

            var result = await _orderService.ProcessOrder(order);

            Assert.NotNull(result);
            Assert.Equal(1, result.Items[0].DeliveryNotification);
        }

        [Fact]
        public async Task Test_SendAlertAndUpdateOrder_SendsUpdate_WhenApiCallIsSuccessful()
        {
            SetupHttpClient(HttpStatusCode.OK, "");

            var order = new Order { OrderId = "1", Items = new List<Item>() };

            await _orderService.SendAlertAndUpdateOrder(order);

            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v != null && v.ToString().Contains("Updated order sent for processing")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()
                ),
                Times.Once
            );
        }

        [Fact]
        public async Task Test_SendAlertAndUpdateOrder_LogsError_WhenApiCallFails()
        {
            SetupHttpClient(HttpStatusCode.BadRequest, "");

            var order = new Order { OrderId = "1", Items = new List<Item>() };

            await _orderService.SendAlertAndUpdateOrder(order);

            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v != null && v.ToString().Contains("Failed to send updated order for processing")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()
                ),
                Times.Once
            );
        }
    }
}