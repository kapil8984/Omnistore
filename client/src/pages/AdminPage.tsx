import React, { useState, useEffect, useCallback } from 'react';
import {
  Package,
  Layers,
  BarChart3,
  Plus,
  Edit,
  Save,
  X,
  CheckCircle,
  AlertCircle,
  Clock,
  Truck,
  XCircle,
  TrendingUp,
  Users,
  DollarSign
} from 'lucide-react';
import {
  Product,
  CreateProductDto,
  UpdateProductDto,
  Order,
  OrderStatus,
  TopCustomerReport
} from '../types';
import { productsApi, ordersApi, reportsApi } from '../services/api';
import Alert from '../components/Alert';
import Pagination from '../components/Pagination';

type AdminTab = 'products' | 'orders' | 'reports';

const AdminPage: React.FC = () => {
  const [activeTab, setActiveTab] = useState<AdminTab>('products');

  // Products Tab State
  const [products, setProducts] = useState<Product[]>([]);
  const [productsLoading, setProductsLoading] = useState(false);
  const [productPage, setProductPage] = useState(1);
  const [productTotalPages, setProductTotalPages] = useState(1);
  const [productSearch, setProductSearch] = useState('');
  const [editingProduct, setEditingProduct] = useState<Product | null>(null);
  const [isCreatingProduct, setIsCreatingProduct] = useState(false);
  const [productForm, setProductForm] = useState<CreateProductDto>({
    sku: '',
    name: '',
    price: 0,
    stockQuantity: 0,
    isActive: true,
  });

  // Orders Tab State
  const [orders, setOrders] = useState<Order[]>([]);
  const [ordersLoading, setOrdersLoading] = useState(false);
  const [ordersPage, setOrdersPage] = useState(1);
  const [ordersTotalPages, setOrdersTotalPages] = useState(1);
  const [updatingOrderId, setUpdatingOrderId] = useState<number | null>(null);

  // Reports Tab State
  const [reports, setReports] = useState<TopCustomerReport[]>([]);
  const [reportsLoading, setReportsLoading] = useState(false);
  const [reportDays, setReportDays] = useState(30);

  // Notifications
  const [feedback, setFeedback] = useState<{ type: 'success' | 'error'; message: string } | null>(null);

  // Fetch Products
  const loadProducts = useCallback(async () => {
    setProductsLoading(true);
    try {
      const res = await productsApi.getProducts({
        page: productPage,
        pageSize: 10,
        search: productSearch || undefined,
      });
      setProducts(res.items);
      setProductTotalPages(res.totalPages);
    } catch (err: unknown) {
      setFeedback({ type: 'error', message: err instanceof Error ? err.message : 'Failed to fetch products' });
    } finally {
      setProductsLoading(false);
    }
  }, [productPage, productSearch]);

  // Fetch Orders
  const loadOrders = useCallback(async () => {
    setOrdersLoading(true);
    try {
      const res = await ordersApi.getMyOrders(ordersPage, 10);
      setOrders(res.items);
      setOrdersTotalPages(res.totalPages);
    } catch (err: unknown) {
      setFeedback({ type: 'error', message: err instanceof Error ? err.message : 'Failed to fetch orders' });
    } finally {
      setOrdersLoading(false);
    }
  }, [ordersPage]);

  // Fetch Top Customers Report
  const loadReports = useCallback(async () => {
    setReportsLoading(true);
    try {
      const res = await reportsApi.getTopCustomers(reportDays);
      setReports(res);
    } catch (err: unknown) {
      setFeedback({ type: 'error', message: err instanceof Error ? err.message : 'Failed to load report' });
    } finally {
      setReportsLoading(false);
    }
  }, [reportDays]);

  useEffect(() => {
    if (activeTab === 'products') loadProducts();
    if (activeTab === 'orders') loadOrders();
    if (activeTab === 'reports') loadReports();
  }, [activeTab, loadProducts, loadOrders, loadReports]);

  // Handle Product Create / Update
  const handleSaveProduct = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      if (editingProduct) {
        await productsApi.updateProduct(editingProduct.id, productForm);
        setFeedback({ type: 'success', message: `Product ${productForm.sku} updated successfully!` });
      } else {
        await productsApi.createProduct(productForm);
        setFeedback({ type: 'success', message: `Product ${productForm.sku} created successfully!` });
      }
      setIsCreatingProduct(false);
      setEditingProduct(null);
      loadProducts();
    } catch (err: unknown) {
      setFeedback({ type: 'error', message: err instanceof Error ? err.message : 'Failed to save product' });
    }
  };

  const openCreateModal = () => {
    setProductForm({ sku: '', name: '', price: 100, stockQuantity: 10, isActive: true });
    setEditingProduct(null);
    setIsCreatingProduct(true);
  };

  const openEditModal = (p: Product) => {
    setEditingProduct(p);
    setProductForm({
      sku: p.sku,
      name: p.name,
      price: p.price,
      stockQuantity: p.stockQuantity,
      isActive: p.isActive,
    });
    setIsCreatingProduct(true);
  };

  // Handle Order Status Transition
  const handleUpdateStatus = async (orderId: number, newStatus: OrderStatus) => {
    setUpdatingOrderId(orderId);
    try {
      const updated = await ordersApi.updateStatus(orderId, newStatus);
      setOrders((prev) => prev.map((o) => (o.id === orderId ? updated : o)));
      setFeedback({ type: 'success', message: `Order #${orderId} status updated to ${newStatus}` });
    } catch (err: unknown) {
      setFeedback({ type: 'error', message: err instanceof Error ? err.message : 'Failed to update order status' });
    } finally {
      setUpdatingOrderId(null);
    }
  };

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
      <div className="flex flex-col sm:flex-row sm:items-center justify-between pb-6 border-b border-slate-700/60 mb-6 gap-4">
        <div>
          <h1 className="text-3xl font-extrabold text-white tracking-tight">Admin Operations Center</h1>
          <p className="text-sm text-slate-400 mt-1">
            Manage inventory, handle order status transitions, and examine real-time customer analytics.
          </p>
        </div>

        {/* Tab Navigation */}
        <div className="flex items-center gap-2 p-1 bg-slate-800/80 rounded-xl border border-slate-700/80">
          <button
            onClick={() => setActiveTab('products')}
            className={`flex items-center gap-2 px-4 py-2 rounded-lg text-sm font-medium transition ${
              activeTab === 'products'
                ? 'bg-indigo-600 text-white shadow-md'
                : 'text-slate-400 hover:text-white'
            }`}
          >
            <Package size={16} />
            <span>Products</span>
          </button>
          <button
            onClick={() => setActiveTab('orders')}
            className={`flex items-center gap-2 px-4 py-2 rounded-lg text-sm font-medium transition ${
              activeTab === 'orders'
                ? 'bg-indigo-600 text-white shadow-md'
                : 'text-slate-400 hover:text-white'
            }`}
          >
            <Layers size={16} />
            <span>Orders</span>
          </button>
          <button
            onClick={() => setActiveTab('reports')}
            className={`flex items-center gap-2 px-4 py-2 rounded-lg text-sm font-medium transition ${
              activeTab === 'reports'
                ? 'bg-indigo-600 text-white shadow-md'
                : 'text-slate-400 hover:text-white'
            }`}
          >
            <BarChart3 size={16} />
            <span>Top Customers</span>
          </button>
        </div>
      </div>

      {feedback && (
        <div className="mb-6">
          <Alert
            type={feedback.type}
            message={feedback.message}
            onClose={() => setFeedback(null)}
          />
        </div>
      )}

      {/* PRODUCTS TAB */}
      {activeTab === 'products' && (
        <div className="space-y-6">
          <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4">
            <div className="w-full sm:w-72">
              <input
                type="text"
                placeholder="Search products by SKU or Name..."
                value={productSearch}
                onChange={(e) => {
                  setProductSearch(e.target.value);
                  setProductPage(1);
                }}
                className="w-full px-4 py-2 bg-slate-800 border border-slate-700 rounded-xl text-white text-sm focus:outline-none focus:border-indigo-500"
              />
            </div>
            <button
              onClick={openCreateModal}
              className="px-4 py-2 bg-indigo-600 hover:bg-indigo-500 text-white text-sm font-medium rounded-xl flex items-center gap-2 shadow-lg shadow-indigo-600/30 transition"
            >
              <Plus size={16} />
              <span>Create Product</span>
            </button>
          </div>

          <div className="bg-slate-800/80 border border-slate-700/80 rounded-2xl overflow-hidden shadow-xl">
            <table className="w-full text-left border-collapse">
              <thead>
                <tr className="border-b border-slate-700 bg-slate-900/40 text-xs font-semibold text-slate-400 uppercase tracking-wider">
                  <th className="py-3.5 px-4">SKU</th>
                  <th className="py-3.5 px-4">Name</th>
                  <th className="py-3.5 px-4 text-right">Price</th>
                  <th className="py-3.5 px-4 text-center">Stock</th>
                  <th className="py-3.5 px-4 text-center">Status</th>
                  <th className="py-3.5 px-4 text-right">Actions</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-700/50 text-sm">
                {productsLoading ? (
                  <tr>
                    <td colSpan={6} className="py-8 text-center text-slate-400">
                      Loading products...
                    </td>
                  </tr>
                ) : products.length === 0 ? (
                  <tr>
                    <td colSpan={6} className="py-8 text-center text-slate-400">
                      No products found.
                    </td>
                  </tr>
                ) : (
                  products.map((p) => (
                    <tr key={p.id} className="hover:bg-slate-700/20 transition">
                      <td className="py-3 px-4 font-mono text-xs text-indigo-400 font-semibold">{p.sku}</td>
                      <td className="py-3 px-4 text-white font-medium">{p.name}</td>
                      <td className="py-3 px-4 text-right text-slate-200">
                        ₹{p.price.toLocaleString('en-IN', { minimumFractionDigits: 2 })}
                      </td>
                      <td className="py-3 px-4 text-center">
                        <span
                          className={`inline-block px-2 py-0.5 rounded text-xs font-semibold ${
                            p.stockQuantity === 0
                              ? 'bg-rose-500/20 text-rose-400'
                              : p.stockQuantity < 5
                              ? 'bg-amber-500/20 text-amber-400'
                              : 'bg-emerald-500/20 text-emerald-400'
                          }`}
                        >
                          {p.stockQuantity}
                        </span>
                      </td>
                      <td className="py-3 px-4 text-center">
                        <span
                          className={`inline-block px-2 py-0.5 rounded text-xs font-semibold ${
                            p.isActive ? 'bg-emerald-500/10 text-emerald-400' : 'bg-slate-700 text-slate-400'
                          }`}
                        >
                          {p.isActive ? 'Active' : 'Inactive'}
                        </span>
                      </td>
                      <td className="py-3 px-4 text-right">
                        <button
                          onClick={() => openEditModal(p)}
                          className="px-3 py-1 bg-slate-700 hover:bg-slate-600 text-slate-200 rounded text-xs font-medium transition inline-flex items-center gap-1"
                        >
                          <Edit size={12} />
                          <span>Edit</span>
                        </button>
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>

          <Pagination
            currentPage={productPage}
            totalPages={productTotalPages}
            onPageChange={(p) => setProductPage(p)}
          />
        </div>
      )}

      {/* ORDERS TAB */}
      {activeTab === 'orders' && (
        <div className="space-y-6">
          <div className="bg-slate-800/80 border border-slate-700/80 rounded-2xl overflow-hidden shadow-xl">
            <table className="w-full text-left border-collapse">
              <thead>
                <tr className="border-b border-slate-700 bg-slate-900/40 text-xs font-semibold text-slate-400 uppercase tracking-wider">
                  <th className="py-3.5 px-4">Order ID</th>
                  <th className="py-3.5 px-4">Customer</th>
                  <th className="py-3.5 px-4 text-right">Total</th>
                  <th className="py-3.5 px-4 text-center">Current Status</th>
                  <th className="py-3.5 px-4 text-center">Change Status (Admin)</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-700/50 text-sm">
                {ordersLoading ? (
                  <tr>
                    <td colSpan={5} className="py-8 text-center text-slate-400">
                      Loading orders...
                    </td>
                  </tr>
                ) : orders.length === 0 ? (
                  <tr>
                    <td colSpan={5} className="py-8 text-center text-slate-400">
                      No orders found.
                    </td>
                  </tr>
                ) : (
                  orders.map((o) => (
                    <tr key={o.id} className="hover:bg-slate-700/20 transition">
                      <td className="py-3 px-4 font-bold text-white">#{o.id}</td>
                      <td className="py-3 px-4">
                        <div className="text-white font-medium">{o.customerName}</div>
                        <div className="text-xs text-slate-400">{o.customerEmail}</div>
                      </td>
                      <td className="py-3 px-4 text-right font-bold text-slate-200">
                        ₹{o.total.toLocaleString('en-IN', { minimumFractionDigits: 2 })}
                      </td>
                      <td className="py-3 px-4 text-center">
                        <span
                          className={`inline-block px-3 py-1 rounded-full text-xs font-semibold ${
                            o.status === 'Pending'
                              ? 'bg-amber-500/10 text-amber-400 border border-amber-500/30'
                              : o.status === 'Confirmed'
                              ? 'bg-blue-500/10 text-blue-400 border border-blue-500/30'
                              : o.status === 'Shipped'
                              ? 'bg-emerald-500/10 text-emerald-400 border border-emerald-500/30'
                              : 'bg-rose-500/10 text-rose-400 border border-rose-500/30'
                          }`}
                        >
                          {o.status}
                        </span>
                      </td>
                      <td className="py-3 px-4 text-center">
                        {o.status === 'Shipped' || o.status === 'Cancelled' ? (
                          <span className="text-xs text-slate-500 italic">Final State</span>
                        ) : (
                          <div className="flex items-center justify-center gap-2">
                            {o.status === 'Pending' && (
                              <button
                                onClick={() => handleUpdateStatus(o.id, 'Confirmed')}
                                disabled={updatingOrderId === o.id}
                                className="px-2.5 py-1 bg-blue-600 hover:bg-blue-500 text-white rounded text-xs font-medium transition"
                              >
                                Confirm
                              </button>
                            )}
                            {o.status === 'Confirmed' && (
                              <button
                                onClick={() => handleUpdateStatus(o.id, 'Shipped')}
                                disabled={updatingOrderId === o.id}
                                className="px-2.5 py-1 bg-emerald-600 hover:bg-emerald-500 text-white rounded text-xs font-medium transition"
                              >
                                Ship
                              </button>
                            )}
                            <button
                              onClick={() => handleUpdateStatus(o.id, 'Cancelled')}
                              disabled={updatingOrderId === o.id}
                              className="px-2.5 py-1 bg-rose-600/80 hover:bg-rose-600 text-white rounded text-xs font-medium transition"
                            >
                              Cancel
                            </button>
                          </div>
                        )}
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>

          <Pagination
            currentPage={ordersPage}
            totalPages={ordersTotalPages}
            onPageChange={(p) => setOrdersPage(p)}
          />
        </div>
      )}

      {/* REPORTS TAB (Part B Query 1) */}
      {activeTab === 'reports' && (
        <div className="space-y-6">
          <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4 bg-slate-800/80 p-4 rounded-2xl border border-slate-700/80">
            <div>
              <h2 className="text-lg font-bold text-white flex items-center gap-2">
                <TrendingUp className="text-emerald-400" size={20} />
                <span>Top 5 Customers by Total Spend (Part B Query 1)</span>
              </h2>
              <p className="text-xs text-slate-400">
                Calculated directly in the database via single LINQ query (Confirmed + Shipped orders only).
              </p>
            </div>

            <div className="flex items-center gap-2">
              <span className="text-xs text-slate-400 font-medium">Period:</span>
              <select
                value={reportDays}
                onChange={(e) => setReportDays(Number(e.target.value))}
                className="bg-slate-900 border border-slate-700 rounded-lg px-3 py-1.5 text-xs text-white focus:outline-none focus:border-indigo-500"
              >
                <option value={7}>Last 7 Days</option>
                <option value={30}>Last 30 Days (Default)</option>
                <option value={90}>Last 90 Days</option>
                <option value={365}>Last 1 Year</option>
              </select>
            </div>
          </div>

          {/* Metric Highlights */}
          <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
            <div className="bg-slate-800/60 border border-slate-700/60 rounded-xl p-4">
              <div className="flex items-center justify-between text-slate-400 mb-2">
                <span className="text-xs uppercase font-semibold">Analyzed Customers</span>
                <Users size={18} className="text-indigo-400" />
              </div>
              <div className="text-2xl font-black text-white">{reports.length}</div>
            </div>

            <div className="bg-slate-800/60 border border-slate-700/60 rounded-xl p-4">
              <div className="flex items-center justify-between text-slate-400 mb-2">
                <span className="text-xs uppercase font-semibold">Total Revenue (Top 5)</span>
                <DollarSign size={18} className="text-emerald-400" />
              </div>
              <div className="text-2xl font-black text-emerald-400">
                ₹{reports.reduce((sum, r) => sum + r.totalSpend, 0).toLocaleString('en-IN', { minimumFractionDigits: 2 })}
              </div>
            </div>

            <div className="bg-slate-800/60 border border-slate-700/60 rounded-xl p-4">
              <div className="flex items-center justify-between text-slate-400 mb-2">
                <span className="text-xs uppercase font-semibold">Top Spender</span>
                <TrendingUp size={18} className="text-amber-400" />
              </div>
              <div className="text-lg font-bold text-white truncate">
                {reports[0]?.customerName || 'N/A'}
              </div>
              <div className="text-xs text-slate-400">
                {reports[0] ? `₹${reports[0].totalSpend.toLocaleString('en-IN')} spend` : 'No data'}
              </div>
            </div>
          </div>

          {/* Report Data Table */}
          <div className="bg-slate-800/80 border border-slate-700/80 rounded-2xl overflow-hidden shadow-xl">
            <table className="w-full text-left border-collapse">
              <thead>
                <tr className="border-b border-slate-700 bg-slate-900/40 text-xs font-semibold text-slate-400 uppercase tracking-wider">
                  <th className="py-3.5 px-4 text-center">Rank</th>
                  <th className="py-3.5 px-4">Customer</th>
                  <th className="py-3.5 px-4 text-center">Orders Placed</th>
                  <th className="py-3.5 px-4 text-right">Total Spend</th>
                  <th className="py-3.5 px-4">Spend Relative Scale</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-700/50 text-sm">
                {reportsLoading ? (
                  <tr>
                    <td colSpan={5} className="py-8 text-center text-slate-400">
                      Running single LINQ database aggregation...
                    </td>
                  </tr>
                ) : reports.length === 0 ? (
                  <tr>
                    <td colSpan={5} className="py-8 text-center text-slate-400">
                      No qualifying Confirmed or Shipped orders in the selected period.
                    </td>
                  </tr>
                ) : (
                  reports.map((r, index) => {
                    const maxSpend = reports[0]?.totalSpend || 1;
                    const percent = Math.min(100, Math.round((r.totalSpend / maxSpend) * 100));

                    return (
                      <tr key={r.customerId} className="hover:bg-slate-700/20 transition">
                        <td className="py-3.5 px-4 text-center font-bold text-slate-400">
                          #{index + 1}
                        </td>
                        <td className="py-3.5 px-4">
                          <div className="font-semibold text-white">{r.customerName}</div>
                          <div className="text-xs text-slate-400">{r.email}</div>
                        </td>
                        <td className="py-3.5 px-4 text-center font-mono text-slate-200">
                          {r.orderCount}
                        </td>
                        <td className="py-3.5 px-4 text-right font-bold text-emerald-400 font-mono">
                          ₹{r.totalSpend.toLocaleString('en-IN', { minimumFractionDigits: 2 })}
                        </td>
                        <td className="py-3.5 px-4 w-1/3">
                          <div className="w-full bg-slate-900 rounded-full h-2.5 overflow-hidden">
                            <div
                              className="bg-gradient-to-r from-indigo-500 to-emerald-400 h-2.5 rounded-full"
                              style={{ width: `${percent}%` }}
                            />
                          </div>
                        </td>
                      </tr>
                    );
                  })
                )}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* CREATE / EDIT PRODUCT MODAL */}
      {isCreatingProduct && (
        <div className="fixed inset-0 z-50 bg-black/70 backdrop-blur-sm flex items-center justify-center p-4">
          <div className="bg-slate-800 border border-slate-700 rounded-2xl w-full max-w-lg p-6 shadow-2xl relative">
            <button
              onClick={() => setIsCreatingProduct(false)}
              className="absolute top-4 right-4 text-slate-400 hover:text-white p-1 rounded-lg"
            >
              <X size={20} />
            </button>

            <h2 className="text-xl font-bold text-white mb-4">
              {editingProduct ? 'Edit Product' : 'Create New Product'}
            </h2>

            <form onSubmit={handleSaveProduct} className="space-y-4">
              <div>
                <label className="block text-xs font-semibold text-slate-300 uppercase mb-1">
                  SKU Code
                </label>
                <input
                  type="text"
                  required
                  value={productForm.sku}
                  onChange={(e) => setProductForm({ ...productForm, sku: e.target.value })}
                  placeholder="e.g. ELEC-016"
                  className="w-full px-4 py-2 bg-slate-900 border border-slate-700 rounded-xl text-white text-sm focus:outline-none focus:border-indigo-500"
                />
              </div>

              <div>
                <label className="block text-xs font-semibold text-slate-300 uppercase mb-1">
                  Product Name
                </label>
                <input
                  type="text"
                  required
                  value={productForm.name}
                  onChange={(e) => setProductForm({ ...productForm, name: e.target.value })}
                  placeholder="e.g. Ultra Ergonomic Mouse"
                  className="w-full px-4 py-2 bg-slate-900 border border-slate-700 rounded-xl text-white text-sm focus:outline-none focus:border-indigo-500"
                />
              </div>

              <div className="grid grid-cols-2 gap-4">
                <div>
                  <label className="block text-xs font-semibold text-slate-300 uppercase mb-1">
                    Price (INR)
                  </label>
                  <input
                    type="number"
                    step="0.01"
                    min="0"
                    required
                    value={productForm.price}
                    onChange={(e) => setProductForm({ ...productForm, price: parseFloat(e.target.value) || 0 })}
                    className="w-full px-4 py-2 bg-slate-900 border border-slate-700 rounded-xl text-white text-sm focus:outline-none focus:border-indigo-500"
                  />
                </div>

                <div>
                  <label className="block text-xs font-semibold text-slate-300 uppercase mb-1">
                    Stock Quantity
                  </label>
                  <input
                    type="number"
                    min="0"
                    required
                    value={productForm.stockQuantity}
                    onChange={(e) => setProductForm({ ...productForm, stockQuantity: parseInt(e.target.value) || 0 })}
                    className="w-full px-4 py-2 bg-slate-900 border border-slate-700 rounded-xl text-white text-sm focus:outline-none focus:border-indigo-500"
                  />
                </div>
              </div>

              <div className="flex items-center gap-3 pt-2">
                <input
                  type="checkbox"
                  id="isActive"
                  checked={productForm.isActive}
                  onChange={(e) => setProductForm({ ...productForm, isActive: e.target.checked })}
                  className="rounded bg-slate-900 border-slate-700 text-indigo-600 focus:ring-0"
                />
                <label htmlFor="isActive" className="text-sm font-medium text-slate-200">
                  Active (Available in customer catalog)
                </label>
              </div>

              <div className="pt-4 flex justify-end gap-3 border-t border-slate-700">
                <button
                  type="button"
                  onClick={() => setIsCreatingProduct(false)}
                  className="px-4 py-2 bg-slate-700 hover:bg-slate-600 text-slate-300 text-sm font-medium rounded-xl transition"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  className="px-5 py-2 bg-indigo-600 hover:bg-indigo-500 text-white text-sm font-semibold rounded-xl flex items-center gap-2 shadow-lg shadow-indigo-600/30 transition"
                >
                  <Save size={16} />
                  <span>{editingProduct ? 'Save Changes' : 'Create Product'}</span>
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};

export default AdminPage;
