import React, { useState, useEffect, useRef } from 'react';
import { useSearchParams } from 'react-router-dom';
import { productsApi } from '../services/api';
import { Product, PagedResult } from '../types';
import { useCart } from '../context/CartContext';
import { Pagination } from '../components/Pagination';
import { Alert } from '../components/Alert';
import { Search, SlidersHorizontal, ShoppingBag, Check, ShieldAlert, Sparkles } from 'lucide-react';

export const CatalogPage: React.FC = () => {
  const [searchParams, setSearchParams] = useSearchParams();
  const { addItem, items: cartItems } = useCart();

  // URL state synchronization
  const searchTerm = searchParams.get('search') || '';
  const minPrice = searchParams.get('minPrice') || '';
  const maxPrice = searchParams.get('maxPrice') || '';
  const inStockOnly = searchParams.get('inStockOnly') === 'true';
  const sortBy = searchParams.get('sortBy') || 'name';
  const isDescending = searchParams.get('isDescending') === 'true';
  const page = parseInt(searchParams.get('page') || '1', 10);

  // Local state for immediate input feedback before debounce
  const [searchInput, setSearchInput] = useState(searchTerm);
  const [data, setData] = useState<PagedResult<Product> | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [addedNotice, setAddedNotice] = useState<number | null>(null);

  const debounceTimerRef = useRef<NodeJS.Timeout | null>(null);

  // Handle debounced search (300ms)
  const handleSearchChange = (value: string) => {
    setSearchInput(value);
    if (debounceTimerRef.current) {
      clearTimeout(debounceTimerRef.current);
    }
    debounceTimerRef.current = setTimeout(() => {
      updateQueryParam('search', value || undefined, true);
    }, 300);
  };

  useEffect(() => {
    setSearchInput(searchTerm);
  }, [searchTerm]);

  // Helper to update query parameters in URL
  const updateQueryParam = (key: string, value: string | undefined, resetPage = false) => {
    const newParams = new URLSearchParams(searchParams);
    if (value !== undefined && value !== '') {
      newParams.set(key, value);
    } else {
      newParams.delete(key);
    }
    if (resetPage) {
      newParams.set('page', '1');
    }
    setSearchParams(newParams);
  };

  // Fetch products from backend whenever URL query parameters change
  useEffect(() => {
    let isCancelled = false;
    const fetchProducts = async () => {
      setLoading(true);
      setError(null);
      try {
        const result = await productsApi.getProducts({
          search: searchTerm || undefined,
          minPrice: minPrice ? parseFloat(minPrice) : undefined,
          maxPrice: maxPrice ? parseFloat(maxPrice) : undefined,
          inStockOnly: inStockOnly || undefined,
          sortBy,
          isDescending,
          page,
          pageSize: 8,
        });

        if (!isCancelled) {
          setData(result);
        }
      } catch (err: unknown) {
        if (!isCancelled) {
          setError(err instanceof Error ? err.message : 'Failed to load products');
        }
      } finally {
        if (!isCancelled) {
          setLoading(false);
        }
      }
    };

    fetchProducts();
    return () => {
      isCancelled = true;
    };
  }, [searchTerm, minPrice, maxPrice, inStockOnly, sortBy, isDescending, page]);

  const handleAddToCart = (product: Product) => {
    addItem(product, 1);
    setAddedNotice(product.id);
    setTimeout(() => {
      setAddedNotice((prev) => (prev === product.id ? null : prev));
    }, 1200);
  };

  const getItemCartQty = (productId: number) => {
    return cartItems.find((ci) => ci.product.id === productId)?.quantity || 0;
  };

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
      {/* Hero Header */}
      <div className="mb-8 flex flex-col md:flex-row md:items-end justify-between gap-4">
        <div>
          <div className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full bg-emerald-500/10 text-emerald-400 text-xs font-semibold mb-3 border border-emerald-500/20">
            <Sparkles className="w-3.5 h-3.5" />
            Curated High-End Electronics & Gear
          </div>
          <h1 className="text-3xl sm:text-4xl font-extrabold text-white tracking-tight">
            Browse Product Catalog
          </h1>
          <p className="text-slate-400 text-sm mt-1">
            Real time server side inventory with optimistic locking protection.
          </p>
        </div>
      </div>

      {error && (
        <div className="mb-6">
          <Alert type="error" message={error} onClose={() => setError(null)} />
        </div>
      )}

      {/* Filter and Search Bar */}
      <div className="bg-slate-800/60 border border-slate-700/60 rounded-2xl p-4 sm:p-5 backdrop-blur-xl mb-8 space-y-4 shadow-xl">
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
          {/* Debounced Search */}
          <div className="relative">
            <Search className="w-4 h-4 text-slate-400 absolute left-3.5 top-1/2 -translate-y-1/2" />
            <input
              type="text"
              value={searchInput}
              onChange={(e) => handleSearchChange(e.target.value)}
              placeholder="Search by name or SKU..."
              className="w-full bg-slate-900/80 border border-slate-700 rounded-xl pl-10 pr-4 py-2.5 text-sm text-white placeholder-slate-500 focus:outline-none focus:ring-2 focus:ring-emerald-500/40 transition-all"
            />
          </div>

          {/* Price Range */}
          <div className="flex items-center gap-2">
            <input
              type="number"
              value={minPrice}
              onChange={(e) => updateQueryParam('minPrice', e.target.value, true)}
              placeholder="Min $"
              className="w-1/2 bg-slate-900/80 border border-slate-700 rounded-xl px-3 py-2.5 text-sm text-white placeholder-slate-500 focus:outline-none focus:ring-2 focus:ring-emerald-500/40"
            />
            <span className="text-slate-500">-</span>
            <input
              type="number"
              value={maxPrice}
              onChange={(e) => updateQueryParam('maxPrice', e.target.value, true)}
              placeholder="Max $"
              className="w-1/2 bg-slate-900/80 border border-slate-700 rounded-xl px-3 py-2.5 text-sm text-white placeholder-slate-500 focus:outline-none focus:ring-2 focus:ring-emerald-500/40"
            />
          </div>

          {/* Sort By Dropdown */}
          <div>
            <select
              value={`${sortBy}_${isDescending}`}
              onChange={(e) => {
                const [sort, desc] = e.target.value.split('_');
                const newParams = new URLSearchParams(searchParams);
                newParams.set('sortBy', sort);
                newParams.set('isDescending', desc);
                newParams.set('page', '1');
                setSearchParams(newParams);
              }}
              className="w-full bg-slate-900/80 border border-slate-700 rounded-xl px-3 py-2.5 text-sm text-white focus:outline-none focus:ring-2 focus:ring-emerald-500/40"
            >
              <option value="name_false">Name (A to Z)</option>
              <option value="name_true">Name (Z to A)</option>
              <option value="price_false">Price: Low to High</option>
              <option value="price_true">Price: High to Low</option>
              <option value="stock_true">Highest Stock First</option>
              <option value="sku_false">SKU</option>
            </select>
          </div>

          {/* In-Stock Toggle */}
          <div className="flex items-center justify-between sm:justify-start gap-3 px-3 py-2 bg-slate-900/50 rounded-xl border border-slate-700/50">
            <label htmlFor="inStockToggle" className="text-xs font-semibold text-slate-300 cursor-pointer select-none">
              In Stock Only
            </label>
            <input
              id="inStockToggle"
              type="checkbox"
              checked={inStockOnly}
              onChange={(e) => updateQueryParam('inStockOnly', e.target.checked ? 'true' : undefined, true)}
              className="w-4 h-4 rounded text-emerald-500 bg-slate-800 border-slate-600 focus:ring-emerald-500/40 cursor-pointer"
            />
          </div>
        </div>
      </div>

      {/* Loading State */}
      {loading && (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-6">
          {[...Array(8)].map((_, i) => (
            <div key={i} className="bg-slate-800/40 border border-slate-800 rounded-2xl p-5 animate-pulse h-64 flex flex-col justify-between">
              <div className="space-y-3">
                <div className="h-4 bg-slate-700/50 rounded w-1/3" />
                <div className="h-6 bg-slate-700/50 rounded w-3/4" />
                <div className="h-4 bg-slate-700/30 rounded w-1/2" />
              </div>
              <div className="h-10 bg-slate-700/50 rounded-xl" />
            </div>
          ))}
        </div>
      )}

      {/* Empty State */}
      {!loading && data?.items.length === 0 && (
        <div className="text-center py-20 px-4 rounded-3xl bg-slate-800/30 border border-slate-800 backdrop-blur-md">
          <div className="w-16 h-16 rounded-2xl bg-slate-800 flex items-center justify-center mx-auto mb-4 text-slate-500">
            <SlidersHorizontal className="w-8 h-8" />
          </div>
          <h3 className="text-lg font-bold text-white">No products found</h3>
          <p className="text-sm text-slate-400 max-w-sm mx-auto mt-1">
            Try adjusting your search criteria, price range, or filter options.
          </p>
          <button
            onClick={() => setSearchParams(new URLSearchParams())}
            className="mt-4 px-4 py-2 rounded-xl bg-slate-700 hover:bg-slate-600 text-xs font-semibold text-white transition-colors"
          >
            Clear all filters
          </button>
        </div>
      )}

      {/* Product Cards Grid */}
      {!loading && data && data.items.length > 0 && (
        <>
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-6">
            {data.items.map((product) => {
              const inCartQty = getItemCartQty(product.id);
              const isOutOfStock = product.stockQuantity <= 0;
              const isJustAdded = addedNotice === product.id;

              return (
                <div
                  key={product.id}
                  className="bg-slate-800/60 border border-slate-700/60 hover:border-slate-600 rounded-2xl p-5 flex flex-col justify-between backdrop-blur-xl transition-all duration-300 hover:-translate-y-1 hover:shadow-2xl hover:shadow-emerald-950/20 group"
                >
                  <div>
                    {/* Header: SKU & Stock Badge */}
                    <div className="flex items-center justify-between gap-2 mb-3">
                      <span className="font-mono text-[11px] font-semibold text-slate-400 bg-slate-900/80 px-2 py-0.5 rounded-md border border-slate-800">
                        {product.sku}
                      </span>

                      {product.stockQuantity > 5 ? (
                        <span className="text-[11px] font-semibold text-emerald-400 bg-emerald-500/10 border border-emerald-500/20 px-2 py-0.5 rounded-full">
                          {product.stockQuantity} in stock
                        </span>
                      ) : product.stockQuantity > 0 ? (
                        <span className="text-[11px] font-semibold text-amber-400 bg-amber-500/10 border border-amber-500/20 px-2 py-0.5 rounded-full flex items-center gap-1 animate-pulse">
                          Only {product.stockQuantity} left!
                        </span>
                      ) : (
                        <span className="text-[11px] font-semibold text-red-400 bg-red-500/10 border border-red-500/20 px-2 py-0.5 rounded-full">
                          Out of Stock
                        </span>
                      )}
                    </div>

                    {/* Product Name */}
                    <h3 className="text-base font-bold text-white group-hover:text-emerald-300 transition-colors line-clamp-2">
                      {product.name}
                    </h3>
                  </div>

                  <div className="pt-6 mt-6 border-t border-slate-700/40">
                    <div className="flex items-baseline justify-between mb-4">
                      <div>
                        <span className="text-xs text-slate-400 block">Price</span>
                        <span className="text-2xl font-extrabold text-white">
                          ${product.price.toLocaleString('en-US', { minimumFractionDigits: 2 })}
                        </span>
                      </div>
                      {product.price > 10000 && (
                        <span className="text-[10px] font-bold text-amber-300 bg-amber-500/10 border border-amber-500/20 px-1.5 py-0.5 rounded">
                          5% Off at Checkout
                        </span>
                      )}
                    </div>

                    {/* Add to Cart Button */}
                    <button
                      onClick={() => handleAddToCart(product)}
                      disabled={isOutOfStock}
                      className={`w-full py-2.5 px-4 rounded-xl font-semibold text-sm flex items-center justify-center gap-2 transition-all ${
                        isOutOfStock
                          ? 'bg-slate-700/50 text-slate-500 cursor-not-allowed border border-slate-700/40'
                          : isJustAdded
                          ? 'bg-emerald-600 text-white'
                          : 'bg-emerald-600 hover:bg-emerald-500 text-white shadow-lg shadow-emerald-600/20 hover:scale-[1.02] active:scale-[0.98]'
                      }`}
                    >
                      {isOutOfStock ? (
                        <>
                          <ShieldAlert className="w-4 h-4" />
                          Sold Out
                        </>
                      ) : isJustAdded ? (
                        <>
                          <Check className="w-4 h-4" />
                          Added to Cart!
                        </>
                      ) : (
                        <>
                          <ShoppingBag className="w-4 h-4" />
                          {inCartQty > 0 ? `Add Another (${inCartQty} in cart)` : 'Add to Cart'}
                        </>
                      )}
                    </button>
                  </div>
                </div>
              );
            })}
          </div>

          {/* Server-Side Pagination */}
          <div className="mt-8">
            <Pagination
              currentPage={data.page}
              totalPages={data.totalPages}
              totalCount={data.totalCount}
              pageSize={data.pageSize}
              onPageChange={(p) => updateQueryParam('page', p.toString())}
            />
          </div>
        </>
      )}
    </div>
  );
};

export default CatalogPage;

