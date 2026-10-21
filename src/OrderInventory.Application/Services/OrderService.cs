using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrderInventory.Application.DTOs.Orders;
using OrderInventory.Application.DTOs.Products;
using OrderInventory.Application.Interfaces;
using OrderInventory.Domain.Entities;
using OrderInventory.Domain.Enums;
using OrderInventory.Domain.Exceptions;

namespace OrderInventory.Application.Services;

public class OrderService : IOrderService
{
    private readonly IApplicationDbContext _context;
    private readonly IDiscountCalculator _discountCalculator;
    private readonly IOrderNotificationService _notificationService;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        IApplicationDbContext context,
        IDiscountCalculator discountCalculator,
        IOrderNotificationService notificationService,
        ILogger<OrderService> logger)
    {
        _context = context;
        _discountCalculator = discountCalculator;
        _notificationService = notificationService;
        _logger = logger;
    }

    public static string ComputeRequestHash(CreateOrderDto dto)
    {
        var normalized = (dto.Items ?? Enumerable.Empty<CreateOrderItemDto>())
            .GroupBy(i => i.ProductId)
            .OrderBy(g => g.Key)
            .Select(g => $"{g.Key}:{g.Sum(x => x.Quantity)}");
        var raw = string.Join(";", normalized);
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes);
    }

    public async Task<OrderDto> CreateOrderAsync(int customerId, CreateOrderDto dto, string? idempotencyKey, CancellationToken cancellationToken = default)
    {
        if (dto.Items == null || !dto.Items.Any())
        {
            throw new BadRequestException("An order must contain at least one item.");
        }

        // Rule 3: Idempotency-Key is mandatory
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new BadRequestException("The 'Idempotency-Key' header is mandatory for order creation.");
        }

        var trimmedKey = idempotencyKey.Trim();
        var currentHash = ComputeRequestHash(dto);

        // Check if this customer already submitted with this key
        var existingRecord = await _context.IdempotencyRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.CustomerId == customerId && r.Key == trimmedKey, cancellationToken);

        if (existingRecord != null)
        {
            // Fingerprint check: Reusing same key with DIFFERENT payload must return 409 Conflict
            if (existingRecord.RequestHash != currentHash)
            {
                _logger.LogWarning("Idempotency key {Key} reused with mismatched payload by Customer {CustomerId}", trimmedKey, customerId);
                throw new ConflictException("The provided Idempotency-Key was previously used with a different request payload.");
            }

            _logger.LogInformation("Idempotent request detected with key {Key}. Returning existing order {OrderId}.", trimmedKey, existingRecord.OrderId);
            try
            {
                var existingDto = JsonSerializer.Deserialize<OrderDto>(existingRecord.ResponseBody);
                if (existingDto != null)
                {
                    return existingDto;
                }
            }
            catch (JsonException)
            {
                // Fallback to loading order from DB
            }

            return await GetOrderDtoInternalAsync(existingRecord.OrderId, cancellationToken);
        }

        // Validate customer
        var customer = await _context.Users.FindAsync(new object[] { customerId }, cancellationToken);
        if (customer == null)
        {
            throw new NotFoundException($"Customer with ID {customerId} was not found.");
        }

        // Consolidate duplicates in request
        var requestedItems = dto.Items
            .GroupBy(i => i.ProductId)
            .Select(g => new CreateOrderItemDto
            {
                ProductId = g.Key,
                Quantity = g.Sum(x => x.Quantity)
            })
            .ToList();

        foreach (var item in requestedItems)
        {
            if (item.Quantity <= 0)
            {
                throw new BadRequestException($"Quantity for product ID {item.ProductId} must be greater than zero.");
            }
        }

        var productIds = requestedItems.Select(i => i.ProductId).ToList();

        // Execute within EF Core Execution Strategy (supports SqlServerRetryingExecutionStrategy)
        var strategy = _context.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await _context.BeginTransactionAsync(cancellationToken);

            try
            {
                var products = await _context.Products
                    .Where(p => productIds.Contains(p.Id))
                    .ToListAsync(cancellationToken);

                if (products.Count != productIds.Count)
                {
                    var missingIds = productIds.Except(products.Select(p => p.Id));
                    throw new BadRequestException($"Products not found: {string.Join(", ", missingIds)}");
                }

                // Rule 1: Validate stock for ALL items before deducting any stock
                foreach (var requested in requestedItems)
                {
                    var product = products.First(p => p.Id == requested.ProductId);

                    if (!product.IsActive)
                    {
                        throw new BadRequestException($"Product '{product.Name}' is not currently active.");
                    }

                    if (product.StockQuantity < requested.Quantity)
                    {
                        throw new ConflictException($"Insufficient stock for product '{product.Name}' (SKU: {product.Sku}). Available: {product.StockQuantity}, Requested: {requested.Quantity}.");
                    }
                }

                // Reserve stock and build order items
                decimal subTotal = 0m;
                var orderItems = new List<OrderItem>();

                foreach (var requested in requestedItems)
                {
                    var product = products.First(p => p.Id == requested.ProductId);
                    product.StockQuantity -= requested.Quantity;

                    var orderItem = new OrderItem
                    {
                        ProductId = product.Id,
                        Quantity = requested.Quantity,
                        UnitPrice = product.Price
                    };

                    orderItems.Add(orderItem);
                    subTotal += (product.Price * requested.Quantity);
                }

                // Rule 5: Server-calculated discounts
                int previousOrderCount = await _context.Orders
                    .CountAsync(o => o.CustomerId == customerId && o.Status != OrderStatus.Cancelled, cancellationToken);

                var (calculatedSubTotal, calculatedDiscount, calculatedTotal) =
                    _discountCalculator.CalculateOrderTotals(subTotal, previousOrderCount);

                var order = new Order
                {
                    CustomerId = customerId,
                    Status = OrderStatus.Pending,
                    SubTotal = calculatedSubTotal,
                    Discount = calculatedDiscount,
                    Total = calculatedTotal,
                    CreatedAt = DateTime.UtcNow,
                    OrderItems = orderItems
                };

                _context.Orders.Add(order);

                // Save order and product stock updates FIRST so database generates order.Id
                await _context.SaveChangesAsync(cancellationToken);

                // Outbox pattern for OrderPlaced notification with actual generated non-zero OrderId
                var outboxMessage = new OutboxMessage
                {
                    Id = Guid.NewGuid(),
                    EventType = "OrderPlaced",
                    Payload = JsonSerializer.Serialize(new
                    {
                        OrderId = order.Id,
                        CustomerId = customerId,
                        Total = calculatedTotal,
                        CreatedAt = order.CreatedAt
                    }),
                    CreatedAt = DateTime.UtcNow
                };
                _context.OutboxMessages.Add(outboxMessage);

                var orderDto = new OrderDto
                {
                    Id = order.Id,
                    CustomerId = customer.Id,
                    CustomerName = customer.FullName,
                    CustomerEmail = customer.Email,
                    Status = order.Status,
                    SubTotal = order.SubTotal,
                    Discount = order.Discount,
                    Total = order.Total,
                    CreatedAt = order.CreatedAt,
                    Items = orderItems.Select(oi =>
                    {
                        var prod = products.First(p => p.Id == oi.ProductId);
                        return new OrderItemDto
                        {
                            Id = oi.Id,
                            ProductId = oi.ProductId,
                            ProductName = prod.Name,
                            Sku = prod.Sku,
                            Quantity = oi.Quantity,
                            UnitPrice = oi.UnitPrice
                        };
                    }).ToList()
                };

                // Rule 3: Record Idempotency Key scoped to customer and fingerprint hash
                var record = new IdempotencyRecord
                {
                    CustomerId = customerId,
                    Key = trimmedKey,
                    RequestHash = currentHash,
                    OrderId = order.Id,
                    StatusCode = 201,
                    ResponseBody = JsonSerializer.Serialize(orderDto),
                    CreatedAt = DateTime.UtcNow
                };
                _context.IdempotencyRecords.Add(record);

                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                // Invalidate product cache so catalog reflects decremented stock immediately
                ProductService.InvalidateProductCache();

                _logger.LogInformation("Order {OrderId} created successfully for customer {CustomerId}. Total: {Total}", order.Id, customerId, order.Total);

                // SignalR notification
                _ = _notificationService.NotifyOrderCreatedAsync(orderDto);

                return orderDto;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogWarning(ex, "Concurrency conflict detected while placing order for customer {CustomerId}.", customerId);
                // Rule 2: Optimistic concurrency conflict must return HTTP 409
                throw new ConflictException("Another customer purchased this item concurrently. Please refresh inventory and try again.");
            }
            catch (DbUpdateException ex)
            {
                await transaction.RollbackAsync(cancellationToken);

                // Handle simultaneous duplicate requests with the same Idempotency-Key
                var existing = await _context.IdempotencyRecords
                    .AsNoTracking()
                    .FirstOrDefaultAsync(r => r.CustomerId == customerId && r.Key == trimmedKey, cancellationToken);

                if (existing != null)
                {
                    if (existing.RequestHash != currentHash)
                    {
                        throw new ConflictException("The provided Idempotency-Key was previously used with a different request payload.");
                    }

                    try
                    {
                        var existingDto = JsonSerializer.Deserialize<OrderDto>(existing.ResponseBody);
                        if (existingDto != null) return existingDto;
                    }
                    catch (JsonException) { }

                    return await GetOrderDtoInternalAsync(existing.OrderId, cancellationToken);
                }

                _logger.LogError(ex, "DbUpdateException occurred while creating order for customer {CustomerId}.", customerId);
                throw;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "Failed to create order for customer {CustomerId}.", customerId);
                throw;
            }
        });
    }

    public async Task<PagedResult<OrderDto>> GetMyOrdersAsync(int customerId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var query = _context.Orders
            .AsNoTracking()
            .Include(o => o.Customer)
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
            .Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.CreatedAt);

        int totalCount = await query.CountAsync(cancellationToken);

        var orders = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var dtos = orders.Select(MapToOrderDto).ToList();

        return new PagedResult<OrderDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<PagedResult<OrderDto>> GetAllOrdersAsync(int page, int pageSize, OrderStatus? status = null, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var query = _context.Orders
            .AsNoTracking()
            .Include(o => o.Customer)
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
            .AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(o => o.Status == status.Value);
        }

        query = query.OrderByDescending(o => o.CreatedAt);

        int totalCount = await query.CountAsync(cancellationToken);

        var orders = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var dtos = orders.Select(MapToOrderDto).ToList();

        return new PagedResult<OrderDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<OrderDto> GetOrderByIdAsync(int orderId, int currentUserId, bool isAdmin, CancellationToken cancellationToken = default)
    {
        var order = await _context.Orders
            .AsNoTracking()
            .Include(o => o.Customer)
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order == null)
        {
            throw new NotFoundException($"Order with ID {orderId} was not found.");
        }

        if (!isAdmin && order.CustomerId != currentUserId)
        {
            throw new ForbiddenException("You do not have permission to view this order.");
        }

        return MapToOrderDto(order);
    }

    public async Task<OrderDto> UpdateOrderStatusAsync(int orderId, OrderStatus newStatus, int currentUserId, bool isAdmin, CancellationToken cancellationToken = default)
    {
        var strategy = _context.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await _context.BeginTransactionAsync(cancellationToken);
            try
            {
                var order = await _context.Orders
                    .Include(o => o.Customer)
                    .Include(o => o.OrderItems)
                        .ThenInclude(oi => oi.Product)
                    .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

                if (order == null)
                {
                    throw new NotFoundException($"Order with ID {orderId} was not found.");
                }

                // Customer permission check
                if (!isAdmin)
                {
                    if (order.CustomerId != currentUserId)
                    {
                        throw new ForbiddenException("You do not have permission to modify this order.");
                    }

                    // Customer may only cancel their own Pending order
                    if (newStatus != OrderStatus.Cancelled || order.Status != OrderStatus.Pending)
                    {
                        throw new BadRequestException("Customers are only permitted to cancel their own Pending orders.");
                    }
                }

                // Rule 4: Status transitions allowed:
                // Pending > Confirmed > Shipped, and Pending or Confirmed > Cancelled.
                // Shipped and Cancelled are final.
                ValidateStatusTransition(order.Status, newStatus);

                // Rule 4: Cancelling restores stock exactly once
                if (newStatus == OrderStatus.Cancelled && order.Status != OrderStatus.Cancelled)
                {
                    foreach (var item in order.OrderItems)
                    {
                        if (item.Product != null)
                        {
                            item.Product.StockQuantity += item.Quantity;
                        }
                    }
                    ProductService.InvalidateProductCache();
                    _logger.LogInformation("Stock restored for cancelled order {OrderId}.", order.Id);
                }

                order.Status = newStatus;
                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                var updatedDto = MapToOrderDto(order);

                // SignalR notification
                _ = _notificationService.NotifyOrderStatusChangedAsync(order.Id, order.Status, order.CustomerId);

                return updatedDto;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogWarning(ex, "Concurrency conflict during order status update for Order {OrderId}", orderId);
                throw new ConflictException("The order or product stock was modified concurrently. Please refresh.");
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    public async Task CancelExpiredPendingOrdersAsync(CancellationToken cancellationToken = default)
    {
        // Rule 6: Automatically cancel orders that stay Pending for more than 30 minutes and restore their stock
        var expirationThreshold = DateTime.UtcNow.AddMinutes(-30);

        var expiredOrderIds = await _context.Orders
            .Where(o => o.Status == OrderStatus.Pending && o.CreatedAt <= expirationThreshold)
            .Select(o => o.Id)
            .ToListAsync(cancellationToken);

        if (!expiredOrderIds.Any())
        {
            return;
        }

        _logger.LogInformation("Found {Count} expired Pending orders to cancel.", expiredOrderIds.Count);

        var strategy = _context.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            bool stockRestoredAny = false;

            foreach (var orderId in expiredOrderIds)
            {
                using var transaction = await _context.BeginTransactionAsync(cancellationToken);
                try
                {
                    // Reload inside transaction to ensure order is still Pending
                    var order = await _context.Orders
                        .Include(o => o.OrderItems)
                            .ThenInclude(oi => oi.Product)
                        .FirstOrDefaultAsync(o => o.Id == orderId && o.Status == OrderStatus.Pending, cancellationToken);

                    if (order == null)
                    {
                        continue; // Status was already changed concurrently by customer or admin
                    }

                    order.Status = OrderStatus.Cancelled;
                    foreach (var item in order.OrderItems)
                    {
                        if (item.Product != null)
                        {
                            item.Product.StockQuantity += item.Quantity;
                            stockRestoredAny = true;
                        }
                    }

                    await _context.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);

                    _logger.LogInformation("Automatically cancelled expired Pending order {OrderId} and restored stock.", order.Id);

                    _ = _notificationService.NotifyOrderStatusChangedAsync(order.Id, order.Status, order.CustomerId);
                }
                catch (DbUpdateConcurrencyException)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    _logger.LogWarning("Concurrency conflict while auto-cancelling expired order {OrderId}; skipped.", orderId);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    _logger.LogError(ex, "Failed to cancel expired order {OrderId}", orderId);
                }
            }

            if (stockRestoredAny)
            {
                ProductService.InvalidateProductCache();
            }
        });
    }

    private static void ValidateStatusTransition(OrderStatus current, OrderStatus next)
    {
        if (current == next)
        {
            return;
        }

        if (current == OrderStatus.Shipped)
        {
            throw new BadRequestException("Cannot change status. 'Shipped' is a final status.");
        }

        if (current == OrderStatus.Cancelled)
        {
            throw new BadRequestException("Cannot change status. 'Cancelled' is a final status.");
        }

        bool isValid = (current, next) switch
        {
            (OrderStatus.Pending, OrderStatus.Confirmed) => true,
            (OrderStatus.Pending, OrderStatus.Cancelled) => true,
            (OrderStatus.Confirmed, OrderStatus.Shipped) => true,
            (OrderStatus.Confirmed, OrderStatus.Cancelled) => true,
            _ => false
        };

        if (!isValid)
        {
            throw new BadRequestException($"Invalid status transition from '{current}' to '{next}'. Allowed transitions: Pending -> Confirmed -> Shipped, or (Pending/Confirmed) -> Cancelled.");
        }
    }

    private async Task<OrderDto> GetOrderDtoInternalAsync(int orderId, CancellationToken cancellationToken)
    {
        var order = await _context.Orders
            .AsNoTracking()
            .Include(o => o.Customer)
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order == null)
        {
            throw new NotFoundException($"Order {orderId} was not found.");
        }

        return MapToOrderDto(order);
    }

    private static OrderDto MapToOrderDto(Order order)
    {
        return new OrderDto
        {
            Id = order.Id,
            CustomerId = order.CustomerId,
            CustomerName = order.Customer?.FullName ?? string.Empty,
            CustomerEmail = order.Customer?.Email ?? string.Empty,
            Status = order.Status,
            SubTotal = order.SubTotal,
            Discount = order.Discount,
            Total = order.Total,
            CreatedAt = order.CreatedAt,
            Items = order.OrderItems.Select(oi => new OrderItemDto
            {
                Id = oi.Id,
                ProductId = oi.ProductId,
                ProductName = oi.Product?.Name ?? string.Empty,
                Sku = oi.Product?.Sku ?? string.Empty,
                Quantity = oi.Quantity,
                UnitPrice = oi.UnitPrice
            }).ToList()
        };
    }
}
