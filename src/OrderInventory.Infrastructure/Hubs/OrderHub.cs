using Microsoft.AspNetCore.SignalR;
using OrderInventory.Application.DTOs.Orders;
using OrderInventory.Application.Interfaces;
using OrderInventory.Domain.Enums;

namespace OrderInventory.Infrastructure.Hubs;

public class OrderHub : Hub
{
    public async Task JoinUserGroup(string userId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
    }

    public async Task JoinAdminGroup()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "admins");
    }
}

public class OrderNotificationService : IOrderNotificationService
{
    private readonly IHubContext<OrderHub> _hubContext;

    public OrderNotificationService(IHubContext<OrderHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task NotifyOrderStatusChangedAsync(int orderId, OrderStatus status, int customerId)
    {
        var payload = new
        {
            orderId,
            status = status.ToString(),
            customerId
        };

        // Notify customer
        await _hubContext.Clients.Group($"user_{customerId}").SendAsync("OrderStatusChanged", payload);

        // Notify admins
        await _hubContext.Clients.Group("admins").SendAsync("OrderStatusChanged", payload);
    }

    public async Task NotifyOrderCreatedAsync(OrderDto order)
    {
        // Notify customer
        await _hubContext.Clients.Group($"user_{order.CustomerId}").SendAsync("OrderCreated", order);

        // Notify admins
        await _hubContext.Clients.Group("admins").SendAsync("OrderCreated", order);
    }
}
