using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using OrderInventory.Application.DTOs.Auth;
using OrderInventory.Application.DTOs.Orders;
using OrderInventory.Application.Interfaces;
using OrderInventory.Application.Services;
using OrderInventory.Domain.Entities;
using OrderInventory.Domain.Enums;
using OrderInventory.Domain.Exceptions;
using OrderInventory.Infrastructure.Persistence;
using OrderInventory.Infrastructure.Security;
using OrderInventory.Infrastructure.Services;
using Xunit;

namespace OrderInventory.UnitTests;

public class ComprehensiveOrderSecurityTests
{
    private readonly Mock<IOrderNotificationService> _notificationMock = new();
    private readonly Mock<ILogger<OrderService>> _orderLoggerMock = new();
    private readonly IDiscountCalculator _discountCalculator = new DiscountCalculator();

    private ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new ApplicationDbContext(options);

        // Seed 2 customers and 1 admin
        context.Users.AddRange(
            new User { Id = 1, FullName = "Customer One", Email = "cust1@test.com", PasswordHash = "hash1", Role = Role.Customer },
            new User { Id = 2, FullName = "Customer Two", Email = "cust2@test.com", PasswordHash = "hash2", Role = Role.Customer },
            new User { Id = 99, FullName = "Admin User", Email = "admin@test.com", PasswordHash = "hashAdmin", Role = Role.Admin }
        );

        // Seed test products
        context.Products.AddRange(
            new Product { Id = 1, Sku = "PROD-1", Name = "Laptop", Price = 1000.00m, StockQuantity = 10, IsActive = true },
            new Product { Id = 2, Sku = "PROD-2", Name = "Mouse", Price = 50.00m, StockQuantity = 5, IsActive = true }
        );

        context.SaveChanges();
        return context;
    }

    [Fact]
    public async Task Security_Registration_PrivilegeEscalation_AlwaysAssignsCustomerRole()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c[It.IsAny<string>()]).Returns("DevelopmentSecretKeyForOmniStoreSystemMustBe32BytesLong!");
        var tokenService = new TokenService(configMock.Object);
        var passwordHasher = new PasswordHasher();
        var authService = new AuthService(context, tokenService, passwordHasher);

        var request = new RegisterRequestDto
        {
            FullName = "Malicious Actor",
            Email = "hacker@evil.com",
            Password = "Password@123",
            Role = Role.Admin // Attempt privilege escalation!
        };

        // Act
        var result = await authService.RegisterAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(Role.Customer, result.User.Role);

        var savedUser = await context.Users.FirstOrDefaultAsync(u => u.Email == "hacker@evil.com");
        Assert.NotNull(savedUser);
        Assert.Equal(Role.Customer, savedUser.Role);
    }

    [Fact]
    public async Task Security_RefreshTokenRevocation_ClearsUserRefreshToken()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c[It.IsAny<string>()]).Returns("DevelopmentSecretKeyForOmniStoreSystemMustBe32BytesLong!");
        var tokenService = new TokenService(configMock.Object);
        var passwordHasher = new PasswordHasher();
        var authService = new AuthService(context, tokenService, passwordHasher);

        var user = await context.Users.FindAsync(1);
        user!.RefreshToken = "VALID_REFRESH_TOKEN";
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
        await context.SaveChangesAsync();

        // Act
        await authService.RevokeRefreshTokenAsync(1);

        // Assert
        var updatedUser = await context.Users.FindAsync(1);
        Assert.Null(updatedUser!.RefreshToken);
        Assert.Null(updatedUser.RefreshTokenExpiryTime);
    }

    [Fact]
    public async Task Idempotency_MissingKey_ThrowsBadRequestException()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var orderService = new OrderService(context, _discountCalculator, _notificationMock.Object, _orderLoggerMock.Object);
        var request = new CreateOrderDto
        {
            Items = new List<CreateOrderItemDto> { new() { ProductId = 1, Quantity = 1 } }
        };

        // Act & Assert (null key)
        var exNull = await Assert.ThrowsAsync<BadRequestException>(() =>
            orderService.CreateOrderAsync(1, request, null));
        Assert.Contains("Idempotency-Key", exNull.Message);

        // Act & Assert (whitespace key)
        var exEmpty = await Assert.ThrowsAsync<BadRequestException>(() =>
            orderService.CreateOrderAsync(1, request, "   "));
        Assert.Contains("Idempotency-Key", exEmpty.Message);
    }

    [Fact]
    public async Task Idempotency_PayloadMismatch_ThrowsConflictException()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var orderService = new OrderService(context, _discountCalculator, _notificationMock.Object, _orderLoggerMock.Object);

        string key = "REUSE-KEY-PAYLOAD-TEST";
        var payload1 = new CreateOrderDto
        {
            Items = new List<CreateOrderItemDto> { new() { ProductId = 1, Quantity = 1 } }
        };

        var payload2 = new CreateOrderDto
        {
            Items = new List<CreateOrderItemDto> { new() { ProductId = 1, Quantity = 2 } } // Different quantity!
        };

        // Act 1: First request with payload 1
        var firstResult = await orderService.CreateOrderAsync(1, payload1, key);
        Assert.NotNull(firstResult);

        // Act 2: Second request with same key but different payload
        var conflictEx = await Assert.ThrowsAsync<ConflictException>(() =>
            orderService.CreateOrderAsync(1, payload2, key));

        Assert.Contains("different request payload", conflictEx.Message);
    }

    [Fact]
    public async Task Idempotency_ScopedToCustomer_DifferentCustomersCanUseSameKey()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var orderService = new OrderService(context, _discountCalculator, _notificationMock.Object, _orderLoggerMock.Object);

        string sharedKey = "SHARED-KEY-ACROSS-CUSTOMERS";
        var request = new CreateOrderDto
        {
            Items = new List<CreateOrderItemDto> { new() { ProductId = 1, Quantity = 1 } }
        };

        // Act: Customer 1 places order with sharedKey
        var order1 = await orderService.CreateOrderAsync(1, request, sharedKey);

        // Customer 2 places order with same sharedKey
        var order2 = await orderService.CreateOrderAsync(2, request, sharedKey);

        // Assert: Distinct orders created for each customer
        Assert.NotEqual(order1.Id, order2.Id);
        Assert.Equal(1, order1.CustomerId);
        Assert.Equal(2, order2.CustomerId);
    }

    [Fact]
    public async Task OutboxMessage_ContainsGeneratedOrderId_NotZero()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var orderService = new OrderService(context, _discountCalculator, _notificationMock.Object, _orderLoggerMock.Object);

        var request = new CreateOrderDto
        {
            Items = new List<CreateOrderItemDto> { new() { ProductId = 1, Quantity = 1 } }
        };

        // Act
        var orderDto = await orderService.CreateOrderAsync(1, request, "OUTBOX-ORDERID-TEST");

        // Assert
        var outbox = await context.OutboxMessages.FirstOrDefaultAsync(m => m.EventType == "OrderPlaced");
        Assert.NotNull(outbox);

        var json = JsonDocument.Parse(outbox.Payload);
        int outboxOrderId = json.RootElement.GetProperty("OrderId").GetInt32();

        Assert.True(outboxOrderId > 0, "OutboxMessage OrderId must be positive and non-zero.");
        Assert.Equal(orderDto.Id, outboxOrderId);
    }

    [Fact]
    public async Task CustomerIsolation_CustomerCannotViewAnotherCustomerOrder()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var orderService = new OrderService(context, _discountCalculator, _notificationMock.Object, _orderLoggerMock.Object);

        var order = new Order { CustomerId = 1, Status = OrderStatus.Pending, SubTotal = 100, Total = 100 };
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        // Act & Assert: Customer 2 attempts to read Customer 1's order
        var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
            orderService.GetOrderByIdAsync(order.Id, currentUserId: 2, isAdmin: false));

        Assert.Contains("permission", ex.Message.ToLower());

        // Admin CAN view Customer 1's order
        var adminView = await orderService.GetOrderByIdAsync(order.Id, currentUserId: 99, isAdmin: true);
        Assert.NotNull(adminView);
    }

    [Fact]
    public async Task CustomerIsolation_CustomerCannotCancelAnotherCustomerOrder()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var orderService = new OrderService(context, _discountCalculator, _notificationMock.Object, _orderLoggerMock.Object);

        var order = new Order { CustomerId = 1, Status = OrderStatus.Pending, SubTotal = 100, Total = 100 };
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        // Act & Assert: Customer 2 attempts to cancel Customer 1's order
        var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
            orderService.UpdateOrderStatusAsync(order.Id, OrderStatus.Cancelled, currentUserId: 2, isAdmin: false));

        Assert.Contains("permission", ex.Message.ToLower());
    }

    [Fact]
    public async Task Admin_GetAllOrders_ReturnsAllCustomerOrdersPaginated()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var orderService = new OrderService(context, _discountCalculator, _notificationMock.Object, _orderLoggerMock.Object);

        context.Orders.AddRange(
            new Order { CustomerId = 1, Status = OrderStatus.Confirmed, SubTotal = 100, Total = 100 },
            new Order { CustomerId = 2, Status = OrderStatus.Shipped, SubTotal = 200, Total = 200 }
        );
        await context.SaveChangesAsync();

        // Act
        var result = await orderService.GetAllOrdersAsync(page: 1, pageSize: 10);

        // Assert
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
    }
}
