using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrderInventory.Application.DTOs.Auth;
using OrderInventory.Application.DTOs.Orders;
using OrderInventory.Domain.Entities;
using OrderInventory.Domain.Enums;
using OrderInventory.Infrastructure.Persistence;
using OrderInventory.Infrastructure.Security;
using Xunit;

namespace OrderInventory.IntegrationTests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Build the service provider to ensure migrations & seed data are executed
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var scopedServices = scope.ServiceProvider;
            var db = scopedServices.GetRequiredService<ApplicationDbContext>();
            var hasher = scopedServices.GetRequiredService<IPasswordHasher>();
            var logger = scopedServices.GetRequiredService<ILogger<CustomWebApplicationFactory>>();

            try
            {
                DbInitializer.SeedAsync(db, hasher, logger).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred seeding the test database.");
            }
        });
    }
}

public class ConcurrencyIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ConcurrencyIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<string> GetCustomerJwtTokenAsync(HttpClient client, string email = "alice@customer.com", string password = "Customer@123")
    {
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto
        {
            Email = email,
            Password = password
        });

        loginResponse.EnsureSuccessStatusCode();
        var options = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());

        var authResult = await loginResponse.Content.ReadFromJsonAsync<AuthResponseDto>(options);
        return authResult!.AccessToken;
    }

    [Fact]
    public async Task Rule2_Concurrency_TwoParallelOrdersForLastUnit_OneGets201_OneGets409()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Create a unique test product with StockQuantity = 1
        var testProduct = new Product
        {
            Sku = $"RACE-{Guid.NewGuid().ToString("N")[..8].ToUpper()}",
            Name = "Last Remaining Collector GPU",
            Price = 999.00m,
            StockQuantity = 1,
            IsActive = true
        };
        db.Products.Add(testProduct);
        await db.SaveChangesAsync();

        var client1 = _factory.CreateClient();
        var client2 = _factory.CreateClient();

        // Get tokens for Alice (Customer 1) and Bob (Customer 2)
        var token1 = await GetCustomerJwtTokenAsync(client1, "alice@customer.com", "Customer@123");
        var token2 = await GetCustomerJwtTokenAsync(client2, "bob@customer.com", "Customer@123");

        client1.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token1);
        client1.DefaultRequestHeaders.Add("Idempotency-Key", $"CONCUR-TEST-{Guid.NewGuid()}");

        client2.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token2);
        client2.DefaultRequestHeaders.Add("Idempotency-Key", $"CONCUR-TEST-{Guid.NewGuid()}");

        var orderRequest = new CreateOrderDto
        {
            Items = new List<CreateOrderItemDto>
            {
                new() { ProductId = testProduct.Id, Quantity = 1 }
            }
        };

        // Act: Fire two simultaneous requests for the last remaining unit
        var task1 = client1.PostAsJsonAsync("/api/orders", orderRequest);
        var task2 = client2.PostAsJsonAsync("/api/orders", orderRequest);

        var responses = await Task.WhenAll(task1, task2);

        var statusCodes = responses.Select(r => r.StatusCode).ToList();
        var body1 = await responses[0].Content.ReadAsStringAsync();
        var body2 = await responses[1].Content.ReadAsStringAsync();

        Assert.True(statusCodes.Contains(HttpStatusCode.Created), $"Expected 201. Responses: [{statusCodes[0]}: {body1}], [{statusCodes[1]}: {body2}]");
        Assert.Contains(HttpStatusCode.Conflict, statusCodes);

        // Verify remaining stock in database is exactly 0 (never negative)
        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var updatedProduct = await verifyDb.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == testProduct.Id);

        Assert.NotNull(updatedProduct);
        Assert.Equal(0, updatedProduct.StockQuantity);
    }
}
