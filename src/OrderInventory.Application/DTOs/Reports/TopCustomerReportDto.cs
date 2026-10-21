namespace OrderInventory.Application.DTOs.Reports;

public class TopCustomerReportDto
{
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public decimal TotalSpend { get; set; }
    public int OrderCount { get; set; }
    public DateTime? LastOrderDate { get; set; }
}
