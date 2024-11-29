using Synapse.Orders.Models;

namespace Synapse.Orders
{
    public interface IOrderService
    {
        Task<Order[]> FetchMedicalEquipmentOrders();
        Task<Order> ProcessOrder(Order order);
        Task SendAlertAndUpdateOrder(Order order);
    }
}