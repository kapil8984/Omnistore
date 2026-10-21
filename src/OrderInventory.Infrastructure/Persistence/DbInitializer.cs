using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrderInventory.Domain.Entities;
using OrderInventory.Domain.Enums;
using OrderInventory.Infrastructure.Security;

namespace OrderInventory.Infrastructure.Persistence;

public static class DbInitializer
{
    public static async Task SeedAsync(ApplicationDbContext context, IPasswordHasher passwordHasher, ILogger logger)
    {
        try
        {
            if (context.Database.IsRelational())
            {
                await context.Database.MigrateAsync();
            }

            // Seed Users (1 Admin, 3 Customers)
            if (!await context.Users.AnyAsync())
            {
                logger.LogInformation("Seeding initial users (1 Admin, 3 Customers)...");

                var users = new List<User>
                {
                    new()
                    {
                        FullName = "Store Administrator",
                        Email = "admin@eshop.com",
                        PasswordHash = passwordHasher.HashPassword("Admin@123"),
                        Role = Role.Admin
                    },
                    new()
                    {
                        FullName = "Alice Walker",
                        Email = "alice@customer.com",
                        PasswordHash = passwordHasher.HashPassword("Customer@123"),
                        Role = Role.Customer
                    },
                    new()
                    {
                        FullName = "Bob Smith",
                        Email = "bob@customer.com",
                        PasswordHash = passwordHasher.HashPassword("Customer@123"),
                        Role = Role.Customer
                    },
                    new()
                    {
                        FullName = "Charlie Brown",
                        Email = "charlie@customer.com",
                        PasswordHash = passwordHasher.HashPassword("Customer@123"),
                        Role = Role.Customer
                    }
                };

                context.Users.AddRange(users);
                await context.SaveChangesAsync();
            }

            // Seed Products (15 Products)
            if (!await context.Products.AnyAsync())
            {
                logger.LogInformation("Seeding 15 initial products...");

                var products = new List<Product>
                {
                    new() { Sku = "LAPTOP-X1", Name = "ThinkPad X1 Carbon Gen 11 14-inch Ultrabook", Price = 12999.00m, StockQuantity = 25, IsActive = true },
                    new() { Sku = "MAC-M3PRO", Name = "MacBook Pro 16-inch M3 Max (36GB / 1TB)", Price = 24999.00m, StockQuantity = 15, IsActive = true },
                    new() { Sku = "PHONE-IP16", Name = "iPhone 16 Pro Max 256GB Titanium", Price = 11999.00m, StockQuantity = 40, IsActive = true },
                    new() { Sku = "KEY-MECH01", Name = "Keychron Q1 Pro Wireless Custom Mechanical Keyboard", Price = 1850.00m, StockQuantity = 50, IsActive = true },
                    new() { Sku = "MOU-MXM03", Name = "Logitech MX Master 3S Wireless Performance Mouse", Price = 999.00m, StockQuantity = 60, IsActive = true },
                    new() { Sku = "MON-4K32", Name = "Dell UltraSharp 32-inch 4K USB-C Hub Monitor", Price = 8499.00m, StockQuantity = 20, IsActive = true },
                    new() { Sku = "HEAD-WH1000", Name = "Sony WH-1000XM5 Wireless Noise-Canceling Headphones", Price = 3499.00m, StockQuantity = 35, IsActive = true },
                    new() { Sku = "TAB-IPAD06", Name = "iPad Air 11-inch M2 Chip Liquid Retina Display", Price = 5999.00m, StockQuantity = 30, IsActive = true },
                    new() { Sku = "DESK-SIT01", Name = "Ergonomic Motorized Dual-Motor Standing Desk 60x30", Price = 4200.00m, StockQuantity = 18, IsActive = true },
                    new() { Sku = "CHAIR-AER", Name = "Herman Miller Aeron Ergonomic Office Chair", Price = 14500.00m, StockQuantity = 12, IsActive = true },
                    new() { Sku = "CAM-C920", Name = "Logitech C920 HD Pro Webcam 1080p", Price = 699.00m, StockQuantity = 80, IsActive = true },
                    new() { Sku = "MIC-YETI", Name = "Blue Yeti Multi-Pattern USB Condenser Microphone", Price = 1199.00m, StockQuantity = 45, IsActive = true },
                    new() { Sku = "SSD-990PRO", Name = "Samsung 990 Pro 2TB NVMe PCIe 4.0 Internal SSD", Price = 1799.00m, StockQuantity = 70, IsActive = true },
                    new() { Sku = "RAM-COR64", Name = "Corsair Vengeance DDR5 64GB (2x32GB) 6000MHz", Price = 2200.00m, StockQuantity = 55, IsActive = true },
                    new() { Sku = "CONCUR-001", Name = "Limited Collector Edition Cyberpunk RTX 4090 GPU", Price = 19999.00m, StockQuantity = 1, IsActive = true }
                };

                context.Products.AddRange(products);
                await context.SaveChangesAsync();
            }

            // Seed Sample Orders if empty (so Top Customers report has historical data)
            if (!await context.Orders.AnyAsync())
            {
                logger.LogInformation("Seeding initial order history for report queries...");
                var alice = await context.Users.FirstAsync(u => u.Email == "alice@customer.com");
                var bob = await context.Users.FirstAsync(u => u.Email == "bob@customer.com");
                var charlie = await context.Users.FirstAsync(u => u.Email == "charlie@customer.com");

                var pMac = await context.Products.FirstAsync(p => p.Sku == "MAC-M3PRO");
                var pLaptop = await context.Products.FirstAsync(p => p.Sku == "LAPTOP-X1");
                var pPhone = await context.Products.FirstAsync(p => p.Sku == "PHONE-IP16");
                var pKey = await context.Products.FirstAsync(p => p.Sku == "KEY-MECH01");
                var pMouse = await context.Products.FirstAsync(p => p.Sku == "MOU-MXM03");

                // Order 1: Alice (Shipped, 10 days ago) - High value discount applied
                // Subtotal = 24999, 5% discount = 1249.95, Total = 23749.05
                var order1 = new Order
                {
                    CustomerId = alice.Id,
                    Status = OrderStatus.Shipped,
                    SubTotal = 24999.00m,
                    Discount = 1249.95m,
                    Total = 23749.05m,
                    CreatedAt = DateTime.UtcNow.AddDays(-10),
                    OrderItems = new List<OrderItem>
                    {
                        new() { ProductId = pMac.Id, Quantity = 1, UnitPrice = 24999.00m }
                    }
                };

                // Order 2: Alice (Confirmed, 5 days ago)
                var order2 = new Order
                {
                    CustomerId = alice.Id,
                    Status = OrderStatus.Confirmed,
                    SubTotal = 2849.00m,
                    Discount = 0.00m,
                    Total = 2849.00m,
                    CreatedAt = DateTime.UtcNow.AddDays(-5),
                    OrderItems = new List<OrderItem>
                    {
                        new() { ProductId = pKey.Id, Quantity = 1, UnitPrice = 1850.00m },
                        new() { ProductId = pMouse.Id, Quantity = 1, UnitPrice = 999.00m }
                    }
                };

                // Order 3: Bob (Shipped, 15 days ago)
                var order3 = new Order
                {
                    CustomerId = bob.Id,
                    Status = OrderStatus.Shipped,
                    SubTotal = 12999.00m,
                    Discount = 649.95m,
                    Total = 12349.05m,
                    CreatedAt = DateTime.UtcNow.AddDays(-15),
                    OrderItems = new List<OrderItem>
                    {
                        new() { ProductId = pLaptop.Id, Quantity = 1, UnitPrice = 12999.00m }
                    }
                };

                // Order 4: Bob (Confirmed, 2 days ago)
                var order4 = new Order
                {
                    CustomerId = bob.Id,
                    Status = OrderStatus.Confirmed,
                    SubTotal = 11999.00m,
                    Discount = 599.95m,
                    Total = 11399.05m,
                    CreatedAt = DateTime.UtcNow.AddDays(-2),
                    OrderItems = new List<OrderItem>
                    {
                        new() { ProductId = pPhone.Id, Quantity = 1, UnitPrice = 11999.00m }
                    }
                };

                // Order 5: Charlie (Pending, 1 hour ago) - Not confirmed/shipped yet
                var order5 = new Order
                {
                    CustomerId = charlie.Id,
                    Status = OrderStatus.Pending,
                    SubTotal = 1850.00m,
                    Discount = 0.00m,
                    Total = 1850.00m,
                    CreatedAt = DateTime.UtcNow.AddHours(-1),
                    OrderItems = new List<OrderItem>
                    {
                        new() { ProductId = pKey.Id, Quantity = 1, UnitPrice = 1850.00m }
                    }
                };

                context.Orders.AddRange(order1, order2, order3, order4, order5);
                await context.SaveChangesAsync();
            }

            logger.LogInformation("Database seed check completed successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error while seeding database.");
            throw;
        }
    }
}
