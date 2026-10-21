# Part C: Code Review & Debugging

> **Notice on Supplied Code Snippet:**
> In the received machine-test email, the section header for `PART C: CODE REVIEW & DEBUGGING` was provided ("The code below 'works' in testing but fails in production. In a file named REVIEW.md, list every problem you find, explain why it is a problem, and write a corrected version."), but the code snippet itself was omitted from the prompt/email body.
> 
> In accordance with the instructions, this limitation is clearly documented here. To demonstrate senior full-stack evaluation and debugging expertise, this document reviews the canonical implementation typically written by developers with 1–3 years experience for this exact checkout and inventory scenario—an implementation that passes standard unit tests in development but fails catastrophically in production.

---

## 1. Typical Flawed Code (Works in Dev, Fails in Production)

```csharp
// Flawed OrderController and Service written without production concurrency/idempotency controls
[HttpPost]
public async Task<IActionResult> CreateOrder([FromBody] OrderRequest request)
{
    // Problem 1: Trusting client-supplied price and discount
    decimal total = request.Items.Sum(i => i.Quantity * i.ClientPrice) - request.ClientDiscount;
    
    var order = new Order
    {
        CustomerId = request.CustomerId,
        Total = total,
        Status = "Pending"
    };
    _context.Orders.Add(order);
    await _context.SaveChangesAsync(); // Partial save!

    foreach (var item in request.Items)
    {
        // Problem 2: N+1 query inside a loop
        var product = await _context.Products.FindAsync(item.ProductId);

        // Problem 3: Non-atomic inventory check without optimistic/pessimistic locking
        if (product.StockQuantity >= item.Quantity)
        {
            product.StockQuantity -= item.Quantity;
            _context.OrderItems.Add(new OrderItem
            {
                OrderId = order.Id,
                ProductId = product.Id,
                Quantity = item.Quantity,
                UnitPrice = item.ClientPrice
            });
            await _context.SaveChangesAsync(); // Problem 4: Multiple non-transactional commits!
        }
        else
        {
            // Problem 5: Partial order created while previous items were already deducted
            return BadRequest("Out of stock");
        }
    }

    return Ok(order);
}
```

---

## 2. Identified Problems & Production Impact

| # | Flaw / Problem | Production Impact | Severity |
|---|----------------|-------------------|----------|
| **1** | **Client-Supplied Prices & Discounts** | A malicious customer can tamper with HTTP request payloads, altering prices to `0.01` or injecting fraudulent discount amounts. Totals must always be derived authoritatively on the server from the product catalog and verified promotional rules. | **Critical (Security / Revenue Loss)** |
| **2** | **Race Condition on Inventory Check (Lack of Concurrency Control)** | Two concurrent requests for the last remaining unit read `StockQuantity = 1` simultaneously. Both pass the check, deduct 1, and save. The store oversells inventory, resulting in stock reaching `0` or negative numbers, leading to unfulfillable customer orders. | **Critical (Data Integrity)** |
| **3** | **Multiple Non-Transactional `SaveChangesAsync()` Calls** | If item 1 has stock and is saved, but item 2 fails stock check or throws a database timeout, item 1's stock is permanently depleted and an orphaned partial order remains in the database. Stock reservation must be strictly atomic within an `IDbContextTransaction`. | **High (Data Inconsistency)** |
| **4** | **N+1 Database Query in Loop** | Calling `FindAsync(item.ProductId)` and `SaveChangesAsync()` inside a `foreach` loop creates `2 * N` round-trips to SQL Server. Under peak traffic (e.g. flash sales), connection pools exhaust, causing high latency and timeout cascades (HTTP 504 / 500). | **High (Performance / Scalability)** |
| **5** | **Lack of Idempotency Protection** | If a customer double-clicks "Place Order" or a mobile client retries a timed-out network request, two duplicate orders are created and stock is deducted twice. An `Idempotency-Key` header with database persistence is required. | **High (Financial / User Experience)** |
| **6** | **Stringly-Typed Order Status** | Using raw string `"Pending"` allows typos (e.g. `"pending"`, `"PENDING"`) and prevents compiler validation of state machine transitions. Enums (`OrderStatus`) with explicit transition rules should be used. | **Medium (Maintainability / Bugs)** |
| **7** | **Raw `Ok(order)` Leaking EF Entities** | Directly serializing EF Core tracking entities can trigger circular reference exceptions during JSON serialization and leak internal columns (e.g., password hashes, concurrency tokens). DTOs must be returned. | **Medium (API Contract / Security)** |
| **8** | **Returning 500 Errors on Concurrency instead of 409 Conflict** | Without catching `DbUpdateConcurrencyException`, EF Core unhandled concurrency conflicts bubble up as HTTP 500 Internal Server Error instead of RFC 7807 HTTP 409 Conflict. | **Medium (Client Error Handling)** |

---

## 3. Corrected Production-Grade Implementation

```csharp
[HttpPost]
[Authorize]
[ProducesResponseType(typeof(OrderDto), StatusCodes.Status201Created)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
public async Task<IActionResult> CreateOrder(
    [FromBody] CreateOrderDto dto,
    [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
    CancellationToken cancellationToken)
{
    // Extract customer ID from authenticated JWT claims (never trust client payload)
    int customerId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // 1. Idempotency Check
    if (!string.IsNullOrWhiteSpace(idempotencyKey))
    {
        var existing = await _context.IdempotencyRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Key == idempotencyKey, cancellationToken);

        if (existing != null)
        {
            var cachedOrder = JsonSerializer.Deserialize<OrderDto>(existing.ResponseBody);
            return Ok(cachedOrder);
        }
    }

    // 2. Fetch all required products in a single bulk query (Eliminates N+1)
    var requestedProductIds = dto.Items.Select(i => i.ProductId).Distinct().ToList();
    
    var strategy = _context.Database.CreateExecutionStrategy();
    return await strategy.ExecuteAsync(async () =>
    {
        using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var products = await _context.Products
                .Where(p => requestedProductIds.Contains(p.Id))
                .ToListAsync(cancellationToken);

            if (products.Count != requestedProductIds.Count)
            {
                return BadRequest(new ProblemDetails { Title = "Invalid Products", Detail = "One or more products were not found." });
            }

            // 3. Validate stock for ALL items upfront before modifying any entity
            foreach (var item in dto.Items)
            {
                var product = products.First(p => p.Id == item.ProductId);
                if (!product.IsActive)
                {
                    return BadRequest(new ProblemDetails { Detail = $"Product '{product.Name}' is not active." });
                }
                if (product.StockQuantity < item.Quantity)
                {
                    // Return 409 Conflict with clear message
                    return Conflict(new ProblemDetails
                    {
                        Status = StatusCodes.Status409Conflict,
                        Title = "Out of Stock",
                        Detail = $"Insufficient stock for '{product.Name}'. Available: {product.StockQuantity}, Requested: {item.Quantity}."
                    });
                }
            }

            // 4. Calculate authoritative server-side prices and promotional discounts
            decimal subTotal = 0m;
            var orderItems = new List<OrderItem>();
            foreach (var item in dto.Items)
            {
                var product = products.First(p => p.Id == item.ProductId);
                
                // Optimistic concurrency token (RowVersion) on Product will detect parallel race conditions
                product.StockQuantity -= item.Quantity;

                orderItems.Add(new OrderItem
                {
                    ProductId = product.Id,
                    Quantity = item.Quantity,
                    UnitPrice = product.Price // Server price only!
                });

                subTotal += (product.Price * item.Quantity);
            }

            // Server-calculated discounts (5% over 10k, 10% on 5th order)
            int orderHistoryCount = await _context.Orders
                .CountAsync(o => o.CustomerId == customerId && o.Status != OrderStatus.Cancelled, cancellationToken);
            
            var (calculatedSubTotal, discount, total) = _discountCalculator.CalculateOrderTotals(subTotal, orderHistoryCount);

            var order = new Order
            {
                CustomerId = customerId,
                Status = OrderStatus.Pending,
                SubTotal = calculatedSubTotal,
                Discount = discount,
                Total = total,
                CreatedAt = DateTime.UtcNow,
                OrderItems = orderItems
            };

            _context.Orders.Add(order);
            await _context.SaveChangesAsync(cancellationToken);

            var orderDto = MapToDto(order, products);

            // 5. Store Idempotency Record in the same transaction
            if (!string.IsNullOrWhiteSpace(idempotencyKey))
            {
                _context.IdempotencyRecords.Add(new IdempotencyRecord
                {
                    Key = idempotencyKey,
                    OrderId = order.Id,
                    StatusCode = StatusCodes.Status201Created,
                    ResponseBody = JsonSerializer.Serialize(orderDto),
                    CreatedAt = DateTime.UtcNow
                });
                await _context.SaveChangesAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);

            return CreatedAtAction(nameof(GetOrderById), new { id = order.Id }, orderDto);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            // Translate database optimistic lock clash into HTTP 409
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Inventory Concurrency Conflict",
                Detail = "Another customer purchased this item concurrently. Please refresh inventory."
            });
        }
        catch (Exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    });
}
```
