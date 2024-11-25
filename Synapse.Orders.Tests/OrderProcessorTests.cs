using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json.Linq;

namespace Synapse.Orders.Tests
{
    public class OrderProcessorTests
    {
        private readonly Mock<IOrderService> _orderServiceMock;
        private readonly Mock<ILogger<OrderProcessor>> _loggerMock;
        private readonly OrderProcessor _orderProcessor;

        public OrderProcessorTests()
        {
            _orderServiceMock = new Mock<IOrderService>();
            _loggerMock = new Mock<ILogger<OrderProcessor>>();
            _orderProcessor = new OrderProcessor(_orderServiceMock.Object, _loggerMock.Object);
        }

        [Fact]
        public async Task Test_ProcessOrdersAsync_ProcessesOrdersSuccessfully()
        {
            var orders = new[]
            {
                new JObject { ["OrderId"] = "1", ["Items"] = new JArray() }
            };

            _orderServiceMock.Setup(s => s.FetchMedicalEquipmentOrders()).ReturnsAsync(orders);
            _orderServiceMock.Setup(s => s.ProcessOrder(It.IsAny<JObject>())).ReturnsAsync((JObject o) => o);
            _orderServiceMock.Setup(s => s.SendAlertAndUpdateOrder(It.IsAny<JObject>())).Returns(Task.CompletedTask);

            await _orderProcessor.ProcessOrdersAsync();

            _orderServiceMock.Verify(s => s.FetchMedicalEquipmentOrders(), Times.Once);
            _orderServiceMock.Verify(s => s.ProcessOrder(It.IsAny<JObject>()), Times.Once);
            _orderServiceMock.Verify(s => s.SendAlertAndUpdateOrder(It.IsAny<JObject>()), Times.Once);
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Results sent to relevant APIs")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()
                ),
                Times.Once
            );
        }

        [Fact]
        public async Task Test_ProcessOrdersAsync_LogsError_WhenExceptionIsThrown()
        {
            _orderServiceMock.Setup(s => s.FetchMedicalEquipmentOrders()).ThrowsAsync(new Exception("Test exception"));

            await _orderProcessor.ProcessOrdersAsync();

            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("An error occurred while processing orders")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()
                ),
                Times.Once
            );
        }
    }
}