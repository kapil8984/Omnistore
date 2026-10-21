-- ============================================================================
-- ORDER & INVENTORY SYSTEM - SQL SERVER QUERIES (PART B)
-- Schema: Users, Products, Orders, OrderItems, IdempotencyRecords, OutboxMessages
-- ============================================================================

-- ----------------------------------------------------------------------------
-- 1. Top 5 customers by total spend (Confirmed + Shipped orders) in the last 30 days
-- ----------------------------------------------------------------------------
SELECT TOP (5)
    u.Id AS CustomerId,
    u.FullName AS CustomerName,
    u.Email,
    SUM(o.Total) AS TotalSpend,
    COUNT(o.Id) AS OrderCount,
    MAX(o.CreatedAt) AS LastOrderDate
FROM Users u
INNER JOIN Orders o ON u.Id = o.CustomerId
WHERE o.Status IN ('Confirmed', 'Shipped')
  AND o.CreatedAt >= DATEADD(DAY, -30, GETUTCDATE())
GROUP BY u.Id, u.FullName, u.Email
ORDER BY TotalSpend DESC;


-- ----------------------------------------------------------------------------
-- 2. Products that have never been ordered
-- ----------------------------------------------------------------------------
SELECT 
    p.Id AS ProductId,
    p.Sku,
    p.Name AS ProductName,
    p.Price,
    p.StockQuantity,
    p.IsActive
FROM Products p
WHERE NOT EXISTS (
    SELECT 1 
    FROM OrderItems oi 
    WHERE oi.ProductId = p.Id
);


-- ----------------------------------------------------------------------------
-- 3. Monthly revenue for the current year, with a running total column (window function)
-- ----------------------------------------------------------------------------
WITH MonthlySales AS (
    SELECT 
        MONTH(o.CreatedAt) AS [MonthNumber],
        DATENAME(MONTH, o.CreatedAt) AS [MonthName],
        SUM(o.Total) AS MonthlyRevenue
    FROM Orders o
    WHERE YEAR(o.CreatedAt) = YEAR(GETUTCDATE())
      AND o.Status IN ('Confirmed', 'Shipped')
    GROUP BY MONTH(o.CreatedAt), DATENAME(MONTH, o.CreatedAt)
)
SELECT 
    MonthNumber,
    MonthName,
    MonthlyRevenue,
    SUM(MonthlyRevenue) OVER (
        ORDER BY MonthNumber 
        ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW
    ) AS RunningTotalRevenue
FROM MonthlySales
ORDER BY MonthNumber;


-- ----------------------------------------------------------------------------
-- 4. Each customer's second-highest order total
--    (Customers with fewer than 2 orders should not appear)
-- ----------------------------------------------------------------------------
WITH RankedOrders AS (
    SELECT 
        o.CustomerId,
        u.FullName AS CustomerName,
        o.Id AS OrderId,
        o.Total,
        o.CreatedAt,
        ROW_NUMBER() OVER (
            PARTITION BY o.CustomerId 
            ORDER BY o.Total DESC, o.Id DESC
        ) AS OrderRank,
        COUNT(o.Id) OVER (
            PARTITION BY o.CustomerId
        ) AS TotalCustomerOrders
    FROM Orders o
    INNER JOIN Users u ON o.CustomerId = u.Id
)
SELECT 
    CustomerId,
    CustomerName,
    OrderId,
    Total AS SecondHighestOrderTotal,
    CreatedAt AS OrderDate
FROM RankedOrders
WHERE OrderRank = 2 
  AND TotalCustomerOrders >= 2;


-- ----------------------------------------------------------------------------
-- 5. Products whose stock is below the average quantity sold per order of that product
-- ----------------------------------------------------------------------------
WITH ProductSalesAvg AS (
    SELECT 
        oi.ProductId,
        AVG(CAST(oi.Quantity AS DECIMAL(18, 2))) AS AvgQuantityPerOrder
    FROM OrderItems oi
    INNER JOIN Orders o ON oi.OrderId = o.Id
    WHERE o.Status IN ('Confirmed', 'Shipped')
    GROUP BY oi.ProductId
)
SELECT 
    p.Id AS ProductId,
    p.Sku,
    p.Name AS ProductName,
    p.StockQuantity,
    ps.AvgQuantityPerOrder
FROM Products p
INNER JOIN ProductSalesAvg ps ON p.Id = ps.ProductId
WHERE p.StockQuantity < ps.AvgQuantityPerOrder;
