# OmniStore - Order & Inventory Management System

A full-stack order and inventory management system built with **ASP.NET Core (.NET 10)** on the backend and **React 18 (TypeScript + Vite + Tailwind CSS)** on the frontend, using **SQL Server** with EF Core.

It handles concurrency-safe stock decrementing, state-driven order transitions, real-time status updates via SignalR, and idempotent order placement.

---

## Tech Stack

- **Backend:** ASP.NET Core Web API (.NET 10)
- **Database / ORM:** SQL Server + Entity Framework Core (Code-First)
- **Frontend:** React 18, TypeScript, Vite, Tailwind CSS, Lucide Icons, Axios
- **Real-time:** SignalR (WebSockets)
- **Testing:** xUnit, Moq, Microsoft.AspNetCore.Mvc.Testing (Integration), Vitest (Frontend)
- **Containers:** Docker & Docker Compose

---

## Project Structure

```
MxpertzInfolab/
├── src/
│   ├── OrderInventory.Domain/          # Core entities, enums, custom exceptions
│   ├── OrderInventory.Application/     # DTOs, business rules, discount logic, interfaces
│   ├── OrderInventory.Infrastructure/  # EF Core DbContext, migrations, auth, background jobs, SignalR
│   └── OrderInventory.Api/             # API controllers, middleware, program startup
├── client/                             # React + Vite frontend application
├── tests/
│   ├── OrderInventory.UnitTests/       # Unit tests (state machine, discounts, idempotency, etc.)
│   └── OrderInventory.IntegrationTests/# Integration tests (concurrency race conditions)
├── docker-compose.yml                  # Docker orchestration (API, Client, SQL Server)
├── QUERIES.sql                         # SQL queries for reports and analytics
└── REVIEW.md                           # Code review analysis and best practices
```

---

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (or compatible .NET 8/9 SDK)
- [Node.js](https://nodejs.org/) (v18+) and npm
- SQL Server (LocalDB or Docker instance)

---

### Step 1: Run the Backend

Open a terminal in the root directory:

```bash
# Restore packages & build
dotnet restore OrderInventorySystem.slnx
dotnet build OrderInventorySystem.slnx

# Run the API
dotnet run --project src/OrderInventory.Api
```

- **API Base URL:** `http://localhost:5069`
- **Swagger Documentation:** `http://localhost:5069/swagger`

> **Note:** When the backend starts for the first time, it automatically applies migrations and seeds initial products and test accounts.

---

### Step 2: Run the Frontend

Open a second terminal and navigate to `client`:

```bash
cd client

# Install dependencies (first time only)
npm install

# Start the Vite dev server
npm run dev
```

- **Frontend App:** `http://localhost:5173`

The frontend connects to the backend at `http://localhost:5069`.

---

### Alternative: Run with Docker Compose

If you have Docker Desktop installed, you can start the entire stack (SQL Server, Backend, Frontend) with a single command:

```bash
docker-compose up --build
```

- Frontend: `http://localhost:3000`
- Backend API / Swagger: `http://localhost:5000/swagger`

---

## Test Accounts & Credentials

The database is pre-seeded with these test users:

| Role | Email | Password | Features / Access |
| :--- | :--- | :--- | :--- |
| **Admin** | `admin@eshop.com` | `Admin@123` | Admin dashboard (`/admin`), manage inventory, view reports, change order statuses |
| **Customer** | `alice@customer.com` | `Customer@123` | Product catalog, cart, checkout, view personal orders (`/orders`) |
| **Customer 2** | `bob@customer.com` | `Customer@123` | Additional test customer account |
| **Customer 3** | `charlie@customer.com` | `Customer@123` | Fresh account |

> On the login page (`/login`), you can click the **Quick Demo Login** buttons to fill credentials automatically.

---

## Key Business Logic & Features

### 1. Atomic Stock Reservation & Concurrency Control
- Stock is verified and decremented within an explicit database transaction. If any item in the cart is out of stock, the transaction rolls back completely.
- Product inventory uses SQL Server row versioning (`RowVersion` byte timestamp). If two customers try to buy the last remaining unit simultaneously, the second write detects the modified row version, raises a concurrency exception, and returns **409 Conflict** with a clear message rather than overselling or crashing.

### 2. Idempotent Order Placement
- The checkout endpoint accepts an `Idempotency-Key` header (the frontend generates a UUID for each submission attempt).
- If a network retry sends the same key, the backend returns the existing order response instead of placing a duplicate order or charging stock twice.

### 3. Order Lifecycle State Machine
- Allowed status flow:
  - `Pending` &rarr; `Confirmed` &rarr; `Shipped`
  - `Pending` or `Confirmed` &rarr; `Cancelled`
- `Shipped` and `Cancelled` are terminal states.
- If an order is cancelled (by customer while pending, by admin, or by the auto-cancellation service), the reserved stock is restored back to the product inventory.

### 4. Automatic Pending Order Expiration
- A background worker (`PendingOrderCancellationBackgroundService`) checks every 60 seconds for orders that have stayed in `Pending` state for over 30 minutes.
- Expired orders are automatically marked `Cancelled`, their stock is restored, and real-time updates are sent via SignalR.

### 5. Server-Side Discount Rules
Discounts are calculated strictly on the server:
- **Volume Discount:** 5% off if subtotal exceeds ₹10,000.
- **Loyalty Discount:** 10% off on every 5th completed order for that customer.

---

## Running Tests

### Backend Tests (xUnit)
```bash
dotnet test --verbosity normal
```
Tests cover:
- Atomic stock reservations and rollback on insufficient inventory
- State machine transitions and invalid transition rejections
- Discount calculations (thresholds and customer order frequency)
- 30-minute pending order auto-cancellation worker
- Concurrency test verifying 409 Conflict under simultaneous race condition

### Frontend Tests (Vitest)
```bash
cd client
npm test
```
Tests cart state calculations, quantity clamping against stock limits, and alert UI components.

---

## License

This project was developed for technical evaluation and reference purposes.
