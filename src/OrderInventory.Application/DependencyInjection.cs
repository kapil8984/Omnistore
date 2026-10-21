using Microsoft.Extensions.DependencyInjection;
using OrderInventory.Application.Interfaces;
using OrderInventory.Application.Services;

namespace OrderInventory.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IDiscountCalculator, DiscountCalculator>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IReportService, ReportService>();

        return services;
    }
}
