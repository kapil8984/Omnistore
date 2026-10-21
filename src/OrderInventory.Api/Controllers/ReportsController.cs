using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderInventory.Application.DTOs.Reports;
using OrderInventory.Application.Interfaces;

namespace OrderInventory.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class ReportsController : ControllerBase
{
    private readonly IReportService _reportService;

    public ReportsController(IReportService reportService)
    {
        _reportService = reportService;
    }

    [HttpGet("top-customers")]
    [ProducesResponseType(typeof(IReadOnlyList<TopCustomerReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetTopCustomers([FromQuery] int days = 30, CancellationToken cancellationToken = default)
    {
        var result = await _reportService.GetTopCustomersAsync(days, cancellationToken);
        return Ok(result);
    }
}
