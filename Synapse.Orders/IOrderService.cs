using Newtonsoft.Json.Linq;

namespace Synapse.Orders
{
    public interface IOrderService
    {
        Task<JObject[]> FetchMedicalEquipmentOrders();
        Task<JObject> ProcessOrder(JObject order);
        Task SendAlertAndUpdateOrder(JObject order);
    }
}