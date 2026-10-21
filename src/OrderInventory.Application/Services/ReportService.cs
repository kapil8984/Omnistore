using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrderInventory.Application.DTOs.Reports;
using OrderInventory.Application.Interfaces;
using OrderInventory.Domain.Enums;

namespace OrderInventory.Application.Services;

public class ReportService : IReportService
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<ReportService> _logger;

    public ReportService(IApplicationDbContext context, ILogger<ReportService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IReadOnlyList<TopCustomerReportDto>> GetTopCustomersAsync(int days, CancellationToken cancellationToken = default)
    {
        if (days <= 0)
        {
            days = 30;
        }

        var cutoff = DateTime.UtcNow.AddDays(-days);

        _logger.LogInformation("Generating top customers report for past {Days} days (cutoff: {Cutoff})", days, cutoff);

        // Single SQL query execution via LINQ grouping and aggregation
        var topCustomers = await _context.Orders
            .AsNoTracking()
            .Where(o => (o.Status == OrderStatus.Confirmed || o.Status == OrderStatus.Shipped) && o.CreatedAt >= cutoff)
            .GroupBy(o => new
            {
                o.CustomerId,
                CustomerName = o.Customer != null ? o.Customer.FullName : string.Empty,
                Email = o.Customer != null ? o.Customer.Email : string.Empty
            })
            .Select(g => new TopCustomerReportDto
            {
                CustomerId = g.Key.CustomerId,
                CustomerName = g.Key.CustomerName,
                Email = g.Key.Email,
                TotalSpend = g.Sum(o => o.Total),
                OrderCount = g.Count(),
                LastOrderDate = g.Max(o => (DateTime?)o.CreatedAt)
            })
            .OrderByDescending(r => r.TotalSpend)
            .Take(5)
            .ToListAsync(cancellationToken);

        return topCustomers;
    }
}
