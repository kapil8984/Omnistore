using OrderInventory.Application.DTOs.Auth;
using OrderInventory.Application.DTOs.Orders;
using OrderInventory.Application.DTOs.Products;
using OrderInventory.Application.DTOs.Reports;
using OrderInventory.Domain.Enums;

namespace OrderInventory.Application.Interfaces;

public interface IAuthService
{
    Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request);
    Task<AuthResponseDto> LoginAsync(LoginRequestDto request);
    Task<AuthResponseDto> RefreshTokenAsync(RefreshTokenRequestDto request);
    Task RevokeRefreshTokenAsync(int userId, CancellationToken cancellationToken = default);
}

public interface IProductService
{
    Task<PagedResult<ProductDto>> GetProductsAsync(ProductQueryParameters parameters, CancellationToken cancellationToken = default);
    Task<ProductDto> GetProductByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<ProductDto> CreateProductAsync(CreateProductDto dto, CancellationToken cancellationToken = default);
    Task<ProductDto> UpdateProductAsync(int id, UpdateProductDto dto, CancellationToken cancellationToken = default);
}

public interface IOrderService
{
    Task<OrderDto> CreateOrderAsync(int customerId, CreateOrderDto dto, string? idempotencyKey, CancellationToken cancellationToken = default);
    Task<PagedResult<OrderDto>> GetMyOrdersAsync(int customerId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<PagedResult<OrderDto>> GetAllOrdersAsync(int page, int pageSize, OrderStatus? status = null, CancellationToken cancellationToken = default);
    Task<OrderDto> GetOrderByIdAsync(int orderId, int currentUserId, bool isAdmin, CancellationToken cancellationToken = default);
    Task<OrderDto> UpdateOrderStatusAsync(int orderId, OrderStatus newStatus, int currentUserId, bool isAdmin, CancellationToken cancellationToken = default);
    Task CancelExpiredPendingOrdersAsync(CancellationToken cancellationToken = default);
}

public interface IReportService
{
    Task<IReadOnlyList<TopCustomerReportDto>> GetTopCustomersAsync(int days, CancellationToken cancellationToken = default);
}

public interface IOrderNotificationService
{
    Task NotifyOrderStatusChangedAsync(int orderId, OrderStatus status, int customerId);
    Task NotifyOrderCreatedAsync(OrderDto order);
}
