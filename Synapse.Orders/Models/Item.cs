namespace Synapse.Orders.Models;

public class Item
{
    public required string Description { get; set; }
    public ItemStatus Status { get; set; }
    public int DeliveryNotification { get; set; }
}