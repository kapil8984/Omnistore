# OmniStore | Enterprise Order & Inventory Management System

A high-performance, production-grade Full-Stack Order and Inventory Management System engineered according to strict enterprise standards. Built with **ASP.NET Core Web API (.NET 10)**, **Entity Framework Core**, **SQL Server**, and **React 18 with TypeScript and Tailwind CSS**.

---

## 📑 Table of Contents
1. [Architecture Overview](#architecture-overview)
2. [Prerequisites & Environment](#prerequisites--environment)
3. [Quick Start & Setup](#quick-start--setup)
   - [Option A: Local Development](#option-a-local-development)
   - [Option B: Docker Compose](#option-b-docker-compose)
4. [Database Migrations & Seeding](#database-migrations--seeding)
5. [Running Automated Tests](#running-automated-tests)
   - [Backend Unit & Integration Tests](#backend-unit--integration-tests)
   - [Frontend Unit Tests](#frontend-unit-tests)
6. [Seeded Credentials](#seeded-credentials)
7. [Core Technical Implementations](#core-technical-implementations)
   - [Rule 1: Atomic Inventory Reservation](#rule-1-atomic-inventory-reservation)
   - [Rule 2: Optimistic Concurrency & HTTP 409 Handling](#rule-2-optimistic-concurrency--http-409-handling)
   - [Rule 3: Database-Backed Idempotency](#rule-3-database-backed-idempotency)
   - [Rule 4: State Machine & Cancellation Stock Restoration](#rule-4-state-machine--cancellation-stock-restoration)
   - [Rule 5: Server-Calculated Discounts](#rule-5-server-calculated-discounts)
   - [Rule 6: Background Pending Order Cancellation](#rule-6-background-pending-order-cancellation)
8. [Part B: SQL & LINQ Optimization](#part-b-sql--linq-optimization)
   - [Generated LINQ SQL (Top 5 Customers)](#generated-linq-sql-top-5-customers)
9. [Part C: Code Review & Production Debugging](#part-c-code-review--production-debugging)
10. [Bonus Features](#bonus-features)
11. [Assumptions & Future Improvements](#assumptions--future-improvements)

---

## Architecture Overview

The solution follows **Clean Architecture** principles to enforce strict separation of concerns, testability, and independence from external frameworks:

```
OmniStore/
├── src/
│   ├── OrderInventory.Domain/          # Enterprise Entities, Enums, Domain Exceptions
│   │   ├── Entities/                   # User, Product, Order, OrderItem, IdempotencyRecord, OutboxMessage
│   │   ├── Enums/                      # OrderStatus, Role
│   │   └── Exceptions/                 # DomainException, ConcurrencyConflictException, InsufficientStockException
│   ├── OrderInventory.Application/     # Use Cases, DTOs, Business Rules, Interfaces
│   │   ├── Common/                     # PagedResult, DiscountCalculator
│   │   ├── DTOs/                       # Auth, Order, Product, Report DTOs
│   │   ├── Interfaces/                 # Service & Infrastructure abstractions
│   │   └── Services/                   # OrderService, ProductService, ReportService
│   ├── OrderInventory.Infrastructure/  # EF Core, SQL Server, Auth, Background Jobs, SignalR
│   │   ├── BackgroundServices/         # PendingOrderCancellationBackgroundService, OutboxBackgroundService
│   │   ├── Hubs/                       # OrderHub (SignalR WebSocket notifications)
│   │   ├── Migrations/                 # EF Core Code-First Migrations
│   │   ├── Persistence/                # ApplicationDbContext, DbInitializer (Seed data)
│   │   └── Services/                   # AuthService, TokenService, PasswordHasher
│   └── OrderInventory.Api/             # ASP.NET Core Web API, Middlewares, Rate Limiting, Controllers
│       ├── Controllers/                # Auth, Products, Orders, Reports
│       └── Middlewares/                # ExceptionHandlingMiddleware (RFC 7807 ProblemDetails)
├── client/                             # React 18 + TypeScript + Vite + Tailwind CSS SPA
│   ├── src/
│   │   ├── components/                 # Navbar, ProtectedRoute, Alert, Pagination
│   │   ├── context/                    # AuthContext, CartContext (localStorage persistence)
│   │   ├── pages/                      # LoginPage, CatalogPage, CartPage, MyOrdersPage, AdminPage
│   │   ├── services/                   # Axios API client (401 refresh retry, 409/429 handlers), SignalR client
│   │   └── test/                       # Vitest unit tests
├── tests/
│   ├── OrderInventory.UnitTests/       # 18 xUnit tests (Rules 1, 3, 4, 5, 6, Discounts, State Machine)
│   └── OrderInventory.IntegrationTests/# WebApplicationFactory test (Parallel race condition for last unit)
├── QUERIES.sql                         # Part B: 5 SQL Server optimized queries
├── REVIEW.md                           # Part C: Code review, failure modes & production rewrite
└── docker-compose.yml                  # Full stack containerization (SQL Server + API + Client)
```

---

## Prerequisites & Environment

- **Backend:** .NET 10 SDK (or .NET 8 / 9 SDK compatible)
- **Database:** Microsoft SQL Server (LocalDB `(localdb)\MSSQLLocalDB` or Docker SQL Server)
- **Frontend:** Node.js (v18+ or v20+) and npm
- **Docker:** Docker Desktop (optional, for containerized execution)

---

## Quick Start & Setup

### Option A: Local Development

#### 1. Backend Setup
```bash
# Clone the repository
git clone <repository-url>
cd OmniStore

# Restore and build backend
dotnet restore OrderInventorySystem.slnx
dotnet build OrderInventorySystem.slnx

# Run database migrations and seed data
dotnet run --project src/OrderInventory.Api
```
The API starts at `http://localhost:5000` (Swagger UI available at `http://localhost:5000/swagger`).
*Note: On initial startup, `DbInitializer` automatically creates the database, applies pending migrations, and seeds the sample data.*

#### 2. Frontend Setup
```bash
cd client

# Install dependencies
npm install

# Start development server
npm run dev
```
The client starts at `http://localhost:5173` with proxy configured for `/api` and `/hubs`.

---

### Option B: Docker Compose

To launch the complete environment (SQL Server 2022, ASP.NET Core API, and Nginx React Client) with a single command:

```bash
docker-compose up --build -d
```

- **Frontend Application:** `http://localhost:3000`
- **Backend API & Swagger:** `http://localhost:5000/swagger`
- **SQL Server:** `localhost:1433` (User: `sa`, Password: `Password123!`)

---

## Database Migrations & Seeding

Migrations are generated using EF Core Code-First. If you need to manually apply or inspect migrations:

```bash
# Update database using dotnet ef
dotnet ef database update --project src/OrderInventory.Infrastructure --startup-project src/OrderInventory.Api
```

### Seed Data Summary
The database is pre-seeded with:
- **1 Admin user**
- **3 Customer users**
- **15 Commercial Products** (Electronics, Workspace, Accessories) with diverse price and inventory levels
- **Historical Orders** across 30 days to populate reporting tables and loyalty discounts.

---

## Running Automated Tests

### Backend Unit & Integration Tests
The backend test suite contains **19 automated tests** with 100% pass rate:
- **18 Unit Tests:** Verifying Rule 1 (Atomic reservation), Rule 3 (Idempotency duplicate protection), Rule 4 (Status transitions & stock restoration), Rule 5 (5% above 10,000 + 10% on every 5th order), Rule 6 (30-minute pending order auto-cancellation).
- **1 Integration Test (`WebApplicationFactory`):** Concurrency integration test executing parallel HTTP requests against the last remaining inventory unit to prove one request receives HTTP 201 Created and the other receives HTTP 409 Conflict.

```bash
# Run all backend tests
dotnet test --verbosity normal
```

### Frontend Unit Tests
Built with **Vitest** and **React Testing Library**:
- `CartContext.test.tsx`: Validates cart item accumulation, strict stock quantity clamping, subtotal calculations, and 5% volume discount threshold.
- `Alert.test.tsx`: Validates alert banner presentation, theme variations, and user dismiss actions.

```bash
cd client
npm test
```

---

## Seeded Credentials

| Role | Email | Password | Purpose |
| :--- | :--- | :--- | :--- |
| **Admin** | `admin@omnistore.com` | `Admin@123!` | Product Catalog CRUD, Order Status State Machine, Top Customers Analytics |
| **Customer 1** | `john@example.com` | `Customer@123!` | Standard customer with historical orders |
| **Customer 2** | `jane@example.com` | `Customer@123!` | Customer ready to test the 5th order 10% discount rule |
| **Customer 3** | `bob@example.com` | `Customer@123!` | Fresh customer account |

---

## Core Technical Implementations

### Rule 1: Atomic Inventory Reservation
When an order is submitted:
1. An explicit database transaction is initiated (`_context.Database.BeginTransactionAsync`).
2. All requested product entities are retrieved from the database.
3. Pre-condition validation checks stock availability across **all** items.
4. If **any** item lacks sufficient stock, an `InsufficientStockException` is thrown, the transaction is rolled back immediately, and **no** stock quantities are altered.
5. If all items are valid, stock is decremented and `Order` + `OrderItem` records are persisted in the same transaction.

### Rule 2: Optimistic Concurrency & HTTP 409 Handling
- Each `Product` record contains a SQL Server `[Timestamp]` column mapped to `byte[] RowVersion`.
- EF Core configures this property with `.IsRowVersion()`.
- When two transactions read the same stock value (e.g. `StockQuantity = 1`) and attempt to decrement it simultaneously:
  - The first transaction commits and SQL Server updates the `RowVersion` timestamp.
  - The second transaction's `UPDATE` statement includes `WHERE [RowVersion] = @originalRowVersion`. Because the `RowVersion` has changed, 0 rows are affected.
  - EF Core raises a `DbUpdateConcurrencyException`.
  - The API's `OrderService` captures this exception and maps it to a `ConcurrencyConflictException`.
  - The `ExceptionHandlingMiddleware` translates this into an RFC 7807 `ProblemDetails` payload with **HTTP 409 Conflict**, preventing any 500 error.
- Verified via `ConcurrencyIntegrationTests.cs`.

### Rule 3: Database-Backed Idempotency
- Order creation requires an `Idempotency-Key` HTTP header.
- The `IdempotencyRecords` table enforces a unique index on `(CustomerId, IdempotencyKey)`.
- When a request arrives:
  1. The table is queried for an existing key belonging to the authenticated customer.
  2. If found, the stored response body is deserialized and returned immediately with the original status code. Stock is never deducted again.
  3. If not found, the order transaction completes, and the JSON result is saved inside `IdempotencyRecords` within the same transaction.
  4. Any duplicate concurrent request with the same key is caught by the unique database constraint and handled gracefully.

### Rule 4: State Machine & Cancellation Stock Restoration
Allowed transitions:
- `Pending` &rarr; `Confirmed` &rarr; `Shipped`
- `Pending` or `Confirmed` &rarr; `Cancelled`
- `Shipped` and `Cancelled` are immutable final states.

**Stock Restoration:**
- When an order transitions to `Cancelled` (either by an Admin, by the Customer while still `Pending`, or by the 30-minute background job), an atomic transaction loops through all `OrderItems` and increments `Product.StockQuantity` by the ordered amounts.
- Invalid transitions immediately reject with **HTTP 400 Bad Request** and an informative error message.

### Rule 5: Server-Calculated Discounts
All calculations occur server-side inside `DiscountCalculator.cs` and are never trusted from client payloads:
- **Volume Discount:** 5% deduction when `SubTotal > 10,000`.
- **Loyalty Discount:** An additional 10% deduction if the customer is placing their **every 5th order** (`(completedOrderCount + 1) % 5 == 0`).
- Calculated using `decimal` arithmetic and rounded to 2 decimal places using `MidpointRounding.AwayFromZero`.

### Rule 6: Background Pending Order Cancellation
- Implemented as an `IHostedService` / `BackgroundService`: `PendingOrderCancellationBackgroundService`.
- Runs periodically (every 60 seconds).
- Identifies orders with `Status == OrderStatus.Pending` where `CreatedAt <= DateTime.UtcNow.AddMinutes(-30)`.
- Transitions each expired order to `Cancelled`, restores inventory stock, writes an Outbox message, and broadcasts the status update via SignalR.

---

## Part B: SQL & LINQ Optimization

All 5 SQL Server queries requested in the specification are authored in `QUERIES.sql`.

### Query 1 LINQ Implementation
Implemented in `src/OrderInventory.Application/Services/ReportService.cs` as a single, optimized LINQ query:

```csharp
var query = _context.Orders
    .AsNoTracking()
    .Where(o => (o.Status == OrderStatus.Confirmed || o.Status == OrderStatus.Shipped) 
             && o.CreatedAt >= cutoffDate)
    .GroupBy(o => new { o.CustomerId, o.Customer.FullName, o.Customer.Email })
    .Select(g => new TopCustomerReportDto
    {
        CustomerId = g.Key.CustomerId,
        CustomerName = g.Key.FullName,
        Email = g.Key.Email,
        TotalSpend = g.Sum(o => o.Total),
        OrderCount = g.Count(),
        LastOrderDate = g.Max(o => o.CreatedAt)
    })
    .OrderByDescending(r => r.TotalSpend)
    .Take(5);
```

### Generated LINQ SQL (Top 5 Customers)
Below is the exact SQL generated by Entity Framework Core from application logs:

```sql
SELECT TOP(@__p_1) 
    [o].[CustomerId], 
    [u].[FullName] AS [CustomerName], 
    [u].[Email], 
    COALESCE(SUM([o].[Total]), 0.0) AS [TotalSpend], 
    COUNT(*) AS [OrderCount], 
    MAX([o].[CreatedAt]) AS [LastOrderDate]
FROM [Orders] AS [o]
INNER JOIN [Users] AS [u] ON [o].[CustomerId] = [u].[Id]
WHERE [o].[Status] IN (1, 2) AND [o].[CreatedAt] >= @__cutoffDate_0
GROUP BY [o].[CustomerId], [u].[FullName], [u].[Email]
ORDER BY [TotalSpend] DESC
```

**Key Optimizations:**
1. **Single Database Roundtrip:** Eliminates N+1 queries by projecting aggregates directly on the server.
2. **Database-Side Aggregation:** Computes `SUM([o].[Total])` and `COUNT(*)` in the database engine rather than loading order records into memory.
3. **Execution Plan:** Can fully leverage an index on `Orders(Status, CreatedAt) INCLUDE (CustomerId, Total)`.

---

## Part C: Code Review & Production Debugging

See [REVIEW.md](file:///d:/MxpertzInfolab/REVIEW.md) for the complete, in-depth analysis.

*Summary of Analysis in `REVIEW.md`:*
1. **Notice on Snippet Omission:** Formally documents that the email prompt omitted the code snippet itself.
2. **Canonical Production Failures Identified & Analyzed:**
   - Lost updates and race conditions due to non-atomic inventory decrement without database locks or `RowVersion`.
   - Client-side discount calculations and price trust vulnerabilities.
   - Non-transactional order creation causing orphaned records or phantom stock decrements on failure.
   - Idempotency memory leaks or lack of distributed locking.
   - Naive background worker memory exhaustion from `ToList()` on unbounded database tables.
3. **Production-Grade Rewrite:** Provides the complete, fully refactored, thread-safe, and transactionally safe implementation matching Part A standards.

---

## Bonus Features

1. **Docker Compose:** Fully orchestrated multi-container deployment (`docker-compose.yml`) containing SQL Server 2022, ASP.NET Core API, and Nginx React frontend.
2. **SignalR Real-Time Updates:** `OrderHub` mapped to `/hubs/orders`. Whenever an order status changes (by Admin, Customer, or 30-min Background Service), events are broadcast live to connected clients.
3. **Transactional Outbox Pattern:** `OutboxMessage` entity saved within the order transaction. `OutboxBackgroundService` processes pending messages asynchronously, guaranteeing reliable event dispatching even if external brokers or websockets experience temporary downtime.
4. **GitHub Actions CI Pipeline:** `.github/workflows/ci.yml` automatically restores, builds, and verifies all backend .NET tests and frontend Vitest tests on push and pull requests.
5. **Frontend Unit Tests:** Implemented using Vitest and React Testing Library covering context state management and alert UI components.

---

## Assumptions & Future Improvements

### Assumptions Made
1. **Currency:** Calculations are performed in standard INR (`₹`) formatted with 2 decimal places using `MidpointRounding.AwayFromZero`.
2. **Order Eligibility for Reports:** As requested in Part B, only `Confirmed` (1) and `Shipped` (2) orders are considered valid customer spend. `Pending` and `Cancelled` orders are excluded.
3. **Customer Self-Service Cancellation:** Customers may only cancel their own orders while the status remains `Pending`. Once `Confirmed` or `Shipped`, cancellations require administrative intervention.

### What Would Be Improved With More Time
1. **Distributed Caching & Idempotency:** Transition `IMemoryCache` and idempotency storage to a distributed Redis cluster for multi-instance horizontal scaling.
2. **Event Broker Integration:** Replace internal Outbox database polling with MassTransit over RabbitMQ / Azure Service Bus for high-throughput microservice messaging.
3. **Elasticsearch / Azure Cognitive Search:** For catalog searching across millions of product SKUs with typo-tolerance and faceted filtering.
4. **End-to-End Testing:** Add Playwright automated end-to-end tests covering the entire login-to-checkout order cycle.
