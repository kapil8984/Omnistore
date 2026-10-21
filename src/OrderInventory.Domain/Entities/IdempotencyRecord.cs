namespace OrderInventory.Domain.Entities;

public class IdempotencyRecord
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public string Key { get; set; } = string.Empty;
    public string RequestHash { get; set; } = string.Empty;
    public int OrderId { get; set; }
    public int StatusCode { get; set; }
    public string ResponseBody { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
