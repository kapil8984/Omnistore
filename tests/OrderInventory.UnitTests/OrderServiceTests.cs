using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using OrderInventory.Application.DTOs.Orders;
using OrderInventory.Application.Interfaces;
using OrderInventory.Application.Services;
using OrderInventory.Domain.Entities;
using OrderInventory.Domain.Enums;
using OrderInventory.Domain.Exceptions;
using OrderInventory.Infrastructure.Persistence;
using Xunit;

namespace OrderInventory.UnitTests;

public class OrderServiceTests
{
    private readonly Mock<IOrderNotificationService> _notificationMock = new();
    private readonly Mock<ILogger<OrderService>> _loggerMock = new();
    private readonly IDiscountCalculator _discountCalculator = new DiscountCalculator();

    private ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new ApplicationDbContext(options);

        // Seed customer
        context.Users.Add(new User
        {
            Id = 1,
            FullName = "John Customer",
            Email = "john@example.com",
            PasswordHash = "hash",
            Role = Role.Customer
        });

        // Seed products
        context.Products.AddRange(
            new Product { Id = 1, Sku = "PROD-1", Name = "Product 1", Price = 100.00m, StockQuantity = 5, IsActive = true },
            new Product { Id = 2, Sku = "PROD-2", Name = "Product 2", Price = 50.00m, StockQuantity = 1, IsActive = true },
            new Product { Id = 3, Sku = "PROD-3", Name = "Out of Stock Product", Price = 20.00m, StockQuantity = 0, IsActive = true }
        );

        context.SaveChanges();
        return context;
    }

    [Fact]
    public async Task Rule1_PlacingOrder_FailsAndDoesNotChangeStock_WhenInsufficientStock()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var orderService = new OrderService(context, _discountCalculator, _notificationMock.Object, _loggerMock.Object);

        var request = new CreateOrderDto
        {
            Items = new List<CreateOrderItemDto>
            {
                new() { ProductId = 1, Quantity = 10 } // Available: 5, Requested: 10
            }
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            orderService.CreateOrderAsync(1, request, null));

        Assert.Contains("Insufficient stock", ex.Message);

        // Verify no stock was reduced
        var product = await context.Products.FindAsync(1);
        Assert.Equal(5, product!.StockQuantity);

        // Verify no order was saved
        Assert.Empty(context.Orders);
    }

    [Fact]
    public async Task Rule1_PlacingOrder_ReservesStock_WhenStockIsSufficient()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var orderService = new OrderService(context, _discountCalculator, _notificationMock.Object, _loggerMock.Object);

        var request = new CreateOrderDto
        {
            Items = new List<CreateOrderItemDto>
            {
                new() { ProductId = 1, Quantity = 2 }
            }
        };

        // Act
        var result = await orderService.CreateOrderAsync(1, request, null);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(OrderStatus.Pending, result.Status);
        Assert.Equal(200.00m, result.Total);

        var product = await context.Products.FindAsync(1);
        Assert.Equal(3, product!.StockQuantity); // 5 - 2 = 3
    }

    [Fact]
    public async Task Rule1_MultipleItems_AtomicRollback_WhenOneItemFailsStock()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var orderService = new OrderService(context, _discountCalculator, _notificationMock.Object, _loggerMock.Object);

        var request = new CreateOrderDto
        {
            Items = new List<CreateOrderItemDto>
            {
                new() { ProductId = 1, Quantity = 2 }, // Product 1 has stock (5)
                new() { ProductId = 3, Quantity = 1 }  // Product 3 has NO stock (0)
            }
        };

        // Act & Assert
        await Assert.ThrowsAsync<ConflictException>(() =>
            orderService.CreateOrderAsync(1, request, null));

        // Product 1 stock must NOT have changed
        var prod1 = await context.Products.FindAsync(1);
        Assert.Equal(5, prod1!.StockQuantity);
        Assert.Empty(context.Orders);
    }

    [Fact]
    public async Task Rule3_Idempotency_RepeatingRequestReturnsOriginalOrder_WithoutDuplicate()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var orderService = new OrderService(context, _discountCalculator, _notificationMock.Object, _loggerMock.Object);

        var request = new CreateOrderDto
        {
            Items = new List<CreateOrderItemDto> { new() { ProductId = 1, Quantity = 2 } }
        };
        string idempotencyKey = "UNIQUE-IDEMP-KEY-12345";

        // Act 1: First attempt
        var firstResult = await orderService.CreateOrderAsync(1, request, idempotencyKey);

        // Act 2: Second duplicate attempt
        var secondResult = await orderService.CreateOrderAsync(1, request, idempotencyKey);

        // Assert
        Assert.Equal(firstResult.Id, secondResult.Id);
        Assert.Equal(firstResult.Total, secondResult.Total);
        Assert.Single(context.Orders); // Only one order created in database
    }

    [Fact]
    public async Task Rule3_Idempotency_RepeatingRequestDoesNotReduceStockTwice()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var orderService = new OrderService(context, _discountCalculator, _notificationMock.Object, _loggerMock.Object);

        var request = new CreateOrderDto
        {
            Items = new List<CreateOrderItemDto> { new() { ProductId = 1, Quantity = 2 } }
        };
        string idempotencyKey = "UNIQUE-IDEMP-KEY-99999";

        // Act
        await orderService.CreateOrderAsync(1, request, idempotencyKey);
        await orderService.CreateOrderAsync(1, request, idempotencyKey);

        // Assert: Stock should only be decremented once (5 - 2 = 3)
        var product = await context.Products.FindAsync(1);
        Assert.Equal(3, product!.StockQuantity);
    }

    [Fact]
    public async Task Rule4_StatusTransition_PendingToConfirmed_Succeeds()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var orderService = new OrderService(context, _discountCalculator, _notificationMock.Object, _loggerMock.Object);

        var order = new Order { CustomerId = 1, Status = OrderStatus.Pending, SubTotal = 100, Total = 100 };
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        // Act
        var updated = await orderService.UpdateOrderStatusAsync(order.Id, OrderStatus.Confirmed, 1, isAdmin: true);

        // Assert
        Assert.Equal(OrderStatus.Confirmed, updated.Status);
    }

    [Fact]
    public async Task Rule4_StatusTransition_ConfirmedToShipped_Succeeds()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var orderService = new OrderService(context, _discountCalculator, _notificationMock.Object, _loggerMock.Object);

        var order = new Order { CustomerId = 1, Status = OrderStatus.Confirmed, SubTotal = 100, Total = 100 };
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        // Act
        var updated = await orderService.UpdateOrderStatusAsync(order.Id, OrderStatus.Shipped, 1, isAdmin: true);

        // Assert
        Assert.Equal(OrderStatus.Shipped, updated.Status);
    }

    [Fact]
    public async Task Rule4_StatusTransition_PendingToCancelled_RestoresStock()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var orderService = new OrderService(context, _discountCalculator, _notificationMock.Object, _loggerMock.Object);

        var product = await context.Products.FindAsync(1);
        product!.StockQuantity = 3; // Initially reserved

        var order = new Order
        {
            CustomerId = 1,
            Status = OrderStatus.Pending,
            SubTotal = 200,
            Total = 200,
            OrderItems = new List<OrderItem>
            {
                new() { ProductId = 1, Quantity = 2, UnitPrice = 100 }
            }
        };
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        // Act: Customer cancels own Pending order
        var result = await orderService.UpdateOrderStatusAsync(order.Id, OrderStatus.Cancelled, 1, isAdmin: false);

        // Assert
        Assert.Equal(OrderStatus.Cancelled, result.Status);

        // Product stock must be restored from 3 to 5
        var reloadedProduct = await context.Products.FindAsync(1);
        Assert.Equal(5, reloadedProduct!.StockQuantity);
    }

    [Fact]
    public async Task Rule4_StatusTransition_ConfirmedToCancelled_RestoresStock()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var orderService = new OrderService(context, _discountCalculator, _notificationMock.Object, _loggerMock.Object);

        var product = await context.Products.FindAsync(1);
        product!.StockQuantity = 2; // Reserved

        var order = new Order
        {
            CustomerId = 1,
            Status = OrderStatus.Confirmed,
            SubTotal = 300,
            Total = 300,
            OrderItems = new List<OrderItem>
            {
                new() { ProductId = 1, Quantity = 3, UnitPrice = 100 }
            }
        };
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        // Act: Admin cancels Confirmed order
        var result = await orderService.UpdateOrderStatusAsync(order.Id, OrderStatus.Cancelled, 99, isAdmin: true);

        // Assert
        Assert.Equal(OrderStatus.Cancelled, result.Status);

        // Product stock must be restored from 2 to 5
        var reloadedProduct = await context.Products.FindAsync(1);
        Assert.Equal(5, reloadedProduct!.StockQuantity);
    }

    [Fact]
    public async Task Rule4_InvalidTransition_ShippedIsFinal_ThrowsBadRequest()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var orderService = new OrderService(context, _discountCalculator, _notificationMock.Object, _loggerMock.Object);

        var order = new Order { CustomerId = 1, Status = OrderStatus.Shipped, SubTotal = 100, Total = 100 };
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            orderService.UpdateOrderStatusAsync(order.Id, OrderStatus.Cancelled, 1, isAdmin: true));

        Assert.Contains("final", ex.Message.ToLower());
    }

    [Fact]
    public async Task Rule4_InvalidTransition_CancelledIsFinal_ThrowsBadRequest()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var orderService = new OrderService(context, _discountCalculator, _notificationMock.Object, _loggerMock.Object);

        var order = new Order { CustomerId = 1, Status = OrderStatus.Cancelled, SubTotal = 100, Total = 100 };
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            orderService.UpdateOrderStatusAsync(order.Id, OrderStatus.Confirmed, 1, isAdmin: true));

        Assert.Contains("final", ex.Message.ToLower());
    }

    [Fact]
    public async Task Rule4_CustomerCannotCancelConfirmedOrder_ThrowsBadRequest()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var orderService = new OrderService(context, _discountCalculator, _notificationMock.Object, _loggerMock.Object);

        var order = new Order { CustomerId = 1, Status = OrderStatus.Confirmed, SubTotal = 100, Total = 100 };
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        // Act & Assert: Customer (isAdmin: false) tries to cancel Confirmed order
        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            orderService.UpdateOrderStatusAsync(order.Id, OrderStatus.Cancelled, 1, isAdmin: false));

        Assert.Contains("only permitted to cancel their own Pending orders", ex.Message);
    }

    [Fact]
    public async Task Rule6_CancelExpiredPendingOrders_CancelsAndRestoresStock_WhenOlderThan30Mins()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var orderService = new OrderService(context, _discountCalculator, _notificationMock.Object, _loggerMock.Object);

        var product = await context.Products.FindAsync(1);
        product!.StockQuantity = 2; // Was 5, 3 reserved

        var expiredOrder = new Order
        {
            CustomerId = 1,
            Status = OrderStatus.Pending,
            CreatedAt = DateTime.UtcNow.AddMinutes(-35), // 35 minutes ago
            SubTotal = 300,
            Total = 300,
            OrderItems = new List<OrderItem>
            {
                new() { ProductId = 1, Quantity = 3, UnitPrice = 100 }
            }
        };

        var recentOrder = new Order
        {
            CustomerId = 1,
            Status = OrderStatus.Pending,
            CreatedAt = DateTime.UtcNow.AddMinutes(-10), // 10 minutes ago
            SubTotal = 100,
            Total = 100,
            OrderItems = new List<OrderItem>
            {
                new() { ProductId = 1, Quantity = 1, UnitPrice = 100 }
            }
        };

        context.Orders.AddRange(expiredOrder, recentOrder);
        await context.SaveChangesAsync();

        // Act: Background service cancels expired pending orders
        await orderService.CancelExpiredPendingOrdersAsync();

        // Assert
        var reloadedExpired = await context.Orders.FindAsync(expiredOrder.Id);
        var reloadedRecent = await context.Orders.FindAsync(recentOrder.Id);

        Assert.Equal(OrderStatus.Cancelled, reloadedExpired!.Status);
        Assert.Equal(OrderStatus.Pending, reloadedRecent!.Status); // Untouched

        // Product stock restored for expired order (2 + 3 = 5)
        var reloadedProd = await context.Products.FindAsync(1);
        Assert.Equal(5, reloadedProd!.StockQuantity);
    }
}
