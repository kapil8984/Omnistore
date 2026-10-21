using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using OrderInventory.Application.DTOs.Products;
using OrderInventory.Application.Interfaces;
using OrderInventory.Domain.Entities;
using OrderInventory.Domain.Exceptions;

using Microsoft.Extensions.Primitives;

namespace OrderInventory.Application.Services;

public class ProductService : IProductService
{
    private readonly IApplicationDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ProductService> _logger;
    private const string ProductsCacheKeyPrefix = "products_query_";
    private static CancellationTokenSource _cacheTokenSource = new();

    public static void InvalidateProductCache()
    {
        var old = Interlocked.Exchange(ref _cacheTokenSource, new CancellationTokenSource());
        try
        {
            old.Cancel();
            old.Dispose();
        }
        catch (ObjectDisposedException) { }
    }

    public ProductService(
        IApplicationDbContext context,
        IMemoryCache cache,
        ILogger<ProductService> logger)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
    }

    public async Task<PagedResult<ProductDto>> GetProductsAsync(ProductQueryParameters parameters, CancellationToken cancellationToken = default)
    {
        string cacheKey = $"{ProductsCacheKeyPrefix}{parameters.Search}_{parameters.MinPrice}_{parameters.MaxPrice}_{parameters.InStockOnly}_{parameters.SortBy}_{parameters.IsDescending}_{parameters.Page}_{parameters.PageSize}";

        if (_cache.TryGetValue(cacheKey, out PagedResult<ProductDto>? cachedResult) && cachedResult != null)
        {
            _logger.LogInformation("Returning products list from cache with key {CacheKey}", cacheKey);
            return cachedResult;
        }

        // Database IQueryable filtering and sorting
        IQueryable<Product> query = _context.Products.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(parameters.Search))
        {
            string term = parameters.Search.Trim().ToLower();
            query = query.Where(p => p.Name.ToLower().Contains(term) || p.Sku.ToLower().Contains(term));
        }

        if (parameters.MinPrice.HasValue)
        {
            query = query.Where(p => p.Price >= parameters.MinPrice.Value);
        }

        if (parameters.MaxPrice.HasValue)
        {
            query = query.Where(p => p.Price <= parameters.MaxPrice.Value);
        }

        if (parameters.InStockOnly.HasValue && parameters.InStockOnly.Value)
        {
            query = query.Where(p => p.StockQuantity > 0 && p.IsActive);
        }

        // Sorting
        query = (parameters.SortBy?.ToLower()) switch
        {
            "price" => parameters.IsDescending ? query.OrderByDescending(p => p.Price) : query.OrderBy(p => p.Price),
            "sku" => parameters.IsDescending ? query.OrderByDescending(p => p.Sku) : query.OrderBy(p => p.Sku),
            "stock" => parameters.IsDescending ? query.OrderByDescending(p => p.StockQuantity) : query.OrderBy(p => p.StockQuantity),
            _ => parameters.IsDescending ? query.OrderByDescending(p => p.Name) : query.OrderBy(p => p.Name)
        };

        int totalCount = await query.CountAsync(cancellationToken);

        int page = Math.Max(1, parameters.Page);
        int pageSize = Math.Clamp(parameters.PageSize, 1, 100);

        List<ProductDto> items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new ProductDto
            {
                Id = p.Id,
                Sku = p.Sku,
                Name = p.Name,
                Price = p.Price,
                StockQuantity = p.StockQuantity,
                IsActive = p.IsActive
            })
            .ToListAsync(cancellationToken);

        var result = new PagedResult<ProductDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };

        var cacheEntryOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
            SlidingExpiration = TimeSpan.FromMinutes(2)
        };
        cacheEntryOptions.AddExpirationToken(new CancellationChangeToken(_cacheTokenSource.Token));

        _cache.Set(cacheKey, result, cacheEntryOptions);
        return result;
    }

    public async Task<ProductDto> GetProductByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var product = await _context.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (product == null)
        {
            throw new NotFoundException($"Product with ID {id} was not found.");
        }

        return new ProductDto
        {
            Id = product.Id,
            Sku = product.Sku,
            Name = product.Name,
            Price = product.Price,
            StockQuantity = product.StockQuantity,
            IsActive = product.IsActive
        };
    }

    public async Task<ProductDto> CreateProductAsync(CreateProductDto dto, CancellationToken cancellationToken = default)
    {
        if (await _context.Products.AnyAsync(p => p.Sku.ToLower() == dto.Sku.Trim().ToLower(), cancellationToken))
        {
            throw new BadRequestException($"Product with SKU '{dto.Sku}' already exists.");
        }

        if (dto.Price < 0)
        {
            throw new BadRequestException("Product price cannot be negative.");
        }

        if (dto.StockQuantity < 0)
        {
            throw new BadRequestException("Product stock quantity cannot be negative.");
        }

        var product = new Product
        {
            Sku = dto.Sku.Trim().ToUpperInvariant(),
            Name = dto.Name.Trim(),
            Price = Math.Round(dto.Price, 2),
            StockQuantity = dto.StockQuantity,
            IsActive = dto.IsActive
        };

        _context.Products.Add(product);
        await _context.SaveChangesAsync(cancellationToken);

        InvalidateCache();
        _logger.LogInformation("Product {ProductId} ({Sku}) created and cache invalidated.", product.Id, product.Sku);

        return new ProductDto
        {
            Id = product.Id,
            Sku = product.Sku,
            Name = product.Name,
            Price = product.Price,
            StockQuantity = product.StockQuantity,
            IsActive = product.IsActive
        };
    }

    public async Task<ProductDto> UpdateProductAsync(int id, UpdateProductDto dto, CancellationToken cancellationToken = default)
    {
        var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (product == null)
        {
            throw new NotFoundException($"Product with ID {id} was not found.");
        }

        if (await _context.Products.AnyAsync(p => p.Id != id && p.Sku.ToLower() == dto.Sku.Trim().ToLower(), cancellationToken))
        {
            throw new BadRequestException($"Product with SKU '{dto.Sku}' already exists.");
        }

        if (dto.Price < 0)
        {
            throw new BadRequestException("Product price cannot be negative.");
        }

        if (dto.StockQuantity < 0)
        {
            throw new BadRequestException("Product stock quantity cannot be negative.");
        }

        product.Sku = dto.Sku.Trim().ToUpperInvariant();
        product.Name = dto.Name.Trim();
        product.Price = Math.Round(dto.Price, 2);
        product.StockQuantity = dto.StockQuantity;
        product.IsActive = dto.IsActive;

        await _context.SaveChangesAsync(cancellationToken);

        InvalidateCache();
        _logger.LogInformation("Product {ProductId} updated and cache invalidated.", product.Id);

        return new ProductDto
        {
            Id = product.Id,
            Sku = product.Sku,
            Name = product.Name,
            Price = product.Price,
            StockQuantity = product.StockQuantity,
            IsActive = product.IsActive
        };
    }

    private void InvalidateCache()
    {
        InvalidateProductCache();
    }
}
