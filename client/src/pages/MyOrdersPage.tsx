import React, { useEffect, useState, useCallback } from 'react';
import { Package, Clock, XCircle, CheckCircle, Truck, AlertTriangle, RefreshCw, LucideIcon } from 'lucide-react';
import { Order, OrderStatus } from '../types';
import { ordersApi } from '../services/api';
import signalrService from '../services/signalr';
import Alert from '../components/Alert';
import Pagination from '../components/Pagination';

const statusBadgeStyles: Record<OrderStatus, { bg: string; text: string; icon: LucideIcon }> = {
  Pending: { bg: 'bg-amber-500/10 border-amber-500/30', text: 'text-amber-400', icon: Clock },
  Confirmed: { bg: 'bg-blue-500/10 border-blue-500/30', text: 'text-blue-400', icon: CheckCircle },
  Shipped: { bg: 'bg-emerald-500/10 border-emerald-500/30', text: 'text-emerald-400', icon: Truck },
  Cancelled: { bg: 'bg-rose-500/10 border-rose-500/30', text: 'text-rose-400', icon: XCircle },
};

const MyOrdersPage: React.FC = () => {
  const [orders, setOrders] = useState<Order[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [cancellingId, setCancellingId] = useState<number | null>(null);
  const [actionSuccess, setActionSuccess] = useState<string | null>(null);

  const fetchOrders = useCallback(async (targetPage: number) => {
    setLoading(true);
    setError(null);
    try {
      const res = await ordersApi.getMyOrders(targetPage, 5);
      setOrders(res.items);
      setPage(res.page);
      setTotalPages(res.totalPages);
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Failed to load your orders.';
      setError(msg);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchOrders(page);
  }, [fetchOrders, page]);

  // Connect to SignalR for live status changes
  useEffect(() => {
    const handleStatusChanged = (orderId: number, newStatus: OrderStatus) => {
      setOrders((prev) =>
        prev.map((o) => (o.id === orderId ? { ...o, status: newStatus } : o))
      );
      setActionSuccess(`Live Update: Order #${orderId} changed to ${newStatus}`);
    };

    signalrService.onOrderStatusChanged(handleStatusChanged);
    signalrService.startConnection().catch(console.error);

    return () => {
      signalrService.offOrderStatusChanged(handleStatusChanged);
    };
  }, []);

  const handleCancelOrder = async (orderId: number) => {
    if (!window.confirm('Are you sure you want to cancel this order? Stock will be restored.')) {
      return;
    }

    setCancellingId(orderId);
    setError(null);
    setActionSuccess(null);

    try {
      const updated = await ordersApi.updateStatus(orderId, 'Cancelled');
      setOrders((prev) => prev.map((o) => (o.id === orderId ? updated : o)));
      setActionSuccess(`Order #${orderId} was cancelled successfully. Reserved stock has been restored.`);
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Failed to cancel order.';
      setError(msg);
    } finally {
      setCancellingId(null);
    }
  };

  return (
    <div className="max-w-6xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 mb-8">
        <div>
          <h1 className="text-3xl font-extrabold text-white tracking-tight">My Orders</h1>
          <p className="text-sm text-slate-400 mt-1">
            Track order status and manage existing orders with real-time updates.
          </p>
        </div>
        <button
          onClick={() => fetchOrders(page)}
          disabled={loading}
          className="inline-flex items-center gap-2 px-4 py-2 bg-slate-800 hover:bg-slate-700 text-slate-300 text-sm font-medium rounded-lg border border-slate-700 transition"
        >
          <RefreshCw size={16} className={loading ? 'animate-spin' : ''} />
          <span>Refresh</span>
        </button>
      </div>

      {actionSuccess && (
        <div className="mb-6">
          <Alert type="success" message={actionSuccess} />
        </div>
      )}

      {error && (
        <div className="mb-6">
          <Alert type="error" message={error} />
        </div>
      )}

      {loading ? (
        <div className="space-y-4">
          {[1, 2, 3].map((i) => (
            <div key={i} className="h-44 bg-slate-800/50 rounded-2xl animate-pulse border border-slate-700/50" />
          ))}
        </div>
      ) : orders.length === 0 ? (
        <div className="text-center py-16 bg-slate-800/40 rounded-2xl border border-slate-700/60 p-8">
          <Package className="w-16 h-16 text-slate-600 mx-auto mb-4" />
          <h3 className="text-lg font-bold text-white mb-1">No orders found</h3>
          <p className="text-slate-400 text-sm mb-6">
            You have not placed any orders yet. Visit the catalog to get started.
          </p>
        </div>
      ) : (
        <div className="space-y-6">
          {orders.map((order) => {
            const Badge = statusBadgeStyles[order.status];
            const StatusIcon = Badge.icon;
            const canCancel = order.status === 'Pending';

            return (
              <div
                key={order.id}
                className="bg-slate-800/80 border border-slate-700/80 rounded-2xl p-6 shadow-lg backdrop-blur transition hover:border-slate-600"
              >
                <div className="flex flex-col sm:flex-row sm:items-center justify-between pb-4 border-b border-slate-700/60 gap-4">
                  <div className="flex items-center gap-4">
                    <span className="text-xl font-bold text-white">Order #{order.id}</span>
                    <span
                      className={`inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-xs font-semibold border ${Badge.bg} ${Badge.text}`}
                    >
                      <StatusIcon size={14} />
                      {order.status}
                    </span>
                  </div>
                  <div className="text-xs text-slate-400">
                    Placed on: {new Date(order.createdAt).toLocaleString()}
                  </div>
                </div>

                {/* Items in Order */}
                <div className="py-4 space-y-2">
                  {order.items.map((item) => (
                    <div key={item.id} className="flex items-center justify-between text-sm py-1">
                      <div className="flex items-center gap-3">
                        <span className="font-mono text-xs text-slate-400 bg-slate-900/60 px-2 py-0.5 rounded">
                          {item.sku}
                        </span>
                        <span className="text-slate-200 font-medium">{item.productName}</span>
                        <span className="text-slate-400 text-xs">x {item.quantity}</span>
                      </div>
                      <div className="text-slate-300 font-mono">
                        ₹{item.lineTotal.toLocaleString('en-IN', { minimumFractionDigits: 2 })}
                      </div>
                    </div>
                  ))}
                </div>

                {/* Pricing Breakdown & Actions */}
                <div className="pt-4 border-t border-slate-700/60 flex flex-col sm:flex-row sm:items-center justify-between gap-4">
                  <div className="flex items-center gap-6 text-sm">
                    <div>
                      <span className="text-xs text-slate-400 block">Subtotal</span>
                      <span className="text-slate-200">
                        ₹{order.subTotal.toLocaleString('en-IN', { minimumFractionDigits: 2 })}
                      </span>
                    </div>
                    {order.discount > 0 && (
                      <div>
                        <span className="text-xs text-emerald-400 block">Discount</span>
                        <span className="text-emerald-400 font-medium">
                          -₹{order.discount.toLocaleString('en-IN', { minimumFractionDigits: 2 })}
                        </span>
                      </div>
                    )}
                    <div>
                      <span className="text-xs text-slate-400 block">Total</span>
                      <span className="text-lg font-bold text-white">
                        ₹{order.total.toLocaleString('en-IN', { minimumFractionDigits: 2 })}
                      </span>
                    </div>
                  </div>

                  <div className="flex items-center gap-3">
                    {canCancel ? (
                      <button
                        onClick={() => handleCancelOrder(order.id)}
                        disabled={cancellingId === order.id}
                        className="px-4 py-2 bg-rose-500/10 hover:bg-rose-500/20 text-rose-400 border border-rose-500/30 rounded-lg text-sm font-semibold transition flex items-center gap-2 disabled:opacity-50"
                      >
                        {cancellingId === order.id ? (
                          <div className="w-4 h-4 border-2 border-rose-400/30 border-t-rose-400 rounded-full animate-spin" />
                        ) : (
                          <>
                            <XCircle size={16} />
                            <span>Cancel Order</span>
                          </>
                        )}
                      </button>
                    ) : (
                      <span className="text-xs text-slate-500 italic">
                        {order.status === 'Cancelled'
                          ? 'Cancelled (Stock restored)'
                          : order.status === 'Shipped'
                          ? 'Shipped (Final state)'
                          : 'Order confirmed'}
                      </span>
                    )}
                  </div>
                </div>
              </div>
            );
          })}

          <Pagination currentPage={page} totalPages={totalPages} onPageChange={(p) => setPage(p)} />
        </div>
      )}
    </div>
  );
};

export default MyOrdersPage;
