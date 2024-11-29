namespace Synapse.Orders.Models;

public class Order
{
    public string OrderId { get; set; } = string.Empty;
    public List<Item> Items { get; set; } = new List<Item>();
}