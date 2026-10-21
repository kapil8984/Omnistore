import React, { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { Trash2, Plus, Minus, ShoppingBag, ArrowRight, ShieldCheck, AlertCircle, CheckCircle2 } from 'lucide-react';
import { useCart } from '../context/CartContext';
import { useAuth } from '../context/AuthContext';
import { ordersApi } from '../services/api';
import Alert from '../components/Alert';

const CartPage: React.FC = () => {
  const { items, updateQuantity, removeItem, clearCart, subTotal, estimatedDiscount, estimatedTotal } = useCart();
  const { isAuthenticated } = useAuth();
  const navigate = useNavigate();

  const [isSubmitting, setIsSubmitting] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [successOrder, setSuccessOrder] = useState<number | null>(null);

  const handleCheckout = async () => {
    if (!isAuthenticated) {
      navigate('/login?redirect=/cart');
      return;
    }

    if (items.length === 0) return;

    setIsSubmitting(true);
    setErrorMessage(null);

    // Generate unique Idempotency-Key (UUID v4)
    const idempotencyKey = typeof crypto !== 'undefined' && crypto.randomUUID
      ? crypto.randomUUID()
      : `key-${Date.now()}-${Math.random().toString(36).substring(2, 9)}`;

    try {
      const orderPayload = {
        items: items.map((i) => ({
          productId: i.product.id,
          quantity: i.quantity,
        })),
      };

      const order = await ordersApi.createOrder(orderPayload, idempotencyKey);
      setSuccessOrder(order.id);
      clearCart();
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'An error occurred during checkout.';
      setErrorMessage(msg);
    } finally {
      setIsSubmitting(false);
    }
  };

  if (successOrder !== null) {
    return (
      <div className="max-w-xl mx-auto px-4 py-16 text-center">
        <div className="w-16 h-16 bg-emerald-500/20 text-emerald-400 rounded-full flex items-center justify-center mx-auto mb-6">
          <CheckCircle2 size={36} />
        </div>
        <h1 className="text-3xl font-extrabold text-white mb-2">Order Confirmed!</h1>
        <p className="text-slate-400 mb-6">
          Your order <span className="text-indigo-400 font-semibold">#{successOrder}</span> has been placed successfully. Stock has been reserved.
        </p>
        <div className="flex flex-col sm:flex-row gap-3 justify-center">
          <Link
            to="/orders"
            className="px-6 py-3 bg-indigo-600 hover:bg-indigo-500 text-white rounded-lg font-medium transition shadow-lg shadow-indigo-600/30"
          >
            View My Orders
          </Link>
          <Link
            to="/"
            className="px-6 py-3 bg-slate-800 hover:bg-slate-700 text-slate-300 rounded-lg font-medium transition"
          >
            Continue Shopping
          </Link>
        </div>
      </div>
    );
  }

  if (items.length === 0) {
    return (
      <div className="max-w-xl mx-auto px-4 py-16 text-center">
        <div className="w-20 h-20 bg-slate-800 text-slate-500 rounded-2xl flex items-center justify-center mx-auto mb-6">
          <ShoppingBag size={40} />
        </div>
        <h2 className="text-2xl font-bold text-white mb-2">Your cart is empty</h2>
        <p className="text-slate-400 mb-8">
          Browse our inventory and discover premium products with real-time stock reservations.
        </p>
        <Link
          to="/"
          className="inline-flex items-center gap-2 px-6 py-3 bg-indigo-600 hover:bg-indigo-500 text-white rounded-xl font-medium transition shadow-lg shadow-indigo-600/20"
        >
          Explore Catalog <ArrowRight size={18} />
        </Link>
      </div>
    );
  }

  return (
    <div className="max-w-6xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
      <h1 className="text-3xl font-extrabold text-white mb-8 tracking-tight">Shopping Cart</h1>

      {errorMessage && (
        <div className="mb-6">
          <Alert type="error" title="Checkout Notice" message={errorMessage} />
        </div>
      )}

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-8">
        {/* Cart Items List */}
        <div className="lg:col-span-2 space-y-4">
          <div className="bg-slate-800/60 border border-slate-700/60 rounded-2xl p-6 backdrop-blur divide-y divide-slate-700/50">
            {items.map(({ product, quantity }) => (
              <div key={product.id} className="py-4 first:pt-0 last:pb-0 flex flex-col sm:flex-row items-start sm:items-center justify-between gap-4">
                <div className="flex-1">
                  <div className="flex items-center gap-3">
                    <span className="font-mono text-xs px-2 py-0.5 rounded bg-slate-700 text-slate-300">
                      {product.sku}
                    </span>
                    <span className="text-xs text-slate-400">Stock: {product.stockQuantity}</span>
                  </div>
                  <h3 className="text-base font-semibold text-white mt-1">{product.name}</h3>
                  <p className="text-sm text-slate-400">
                    ₹{product.price.toLocaleString('en-IN', { minimumFractionDigits: 2 })} each
                  </p>
                </div>

                {/* Quantity Controls */}
                <div className="flex items-center gap-4 w-full sm:w-auto justify-between sm:justify-end">
                  <div className="flex items-center border border-slate-700 rounded-lg bg-slate-900/60 overflow-hidden">
                    <button
                      onClick={() => updateQuantity(product.id, quantity - 1)}
                      className="p-2 text-slate-400 hover:text-white hover:bg-slate-800 transition"
                      aria-label="Decrease quantity"
                    >
                      <Minus size={14} />
                    </button>
                    <span className="px-3 text-sm font-semibold text-white min-w-[2.5rem] text-center">
                      {quantity}
                    </span>
                    <button
                      onClick={() => updateQuantity(product.id, quantity + 1)}
                      disabled={quantity >= product.stockQuantity}
                      className="p-2 text-slate-400 hover:text-white hover:bg-slate-800 disabled:opacity-40 disabled:hover:bg-transparent transition"
                      aria-label="Increase quantity"
                    >
                      <Plus size={14} />
                    </button>
                  </div>

                  <div className="text-right min-w-[5rem]">
                    <span className="text-base font-bold text-white">
                      ₹{(product.price * quantity).toLocaleString('en-IN', { minimumFractionDigits: 2 })}
                    </span>
                  </div>

                  <button
                    onClick={() => removeItem(product.id)}
                    className="p-2 text-slate-500 hover:text-rose-400 hover:bg-rose-500/10 rounded-lg transition"
                    title="Remove item"
                  >
                    <Trash2 size={18} />
                  </button>
                </div>
              </div>
            ))}
          </div>

          <div className="flex justify-between items-center text-sm text-slate-400 px-2">
            <span>Prices are inclusive of standard server calculations.</span>
            <button
              onClick={clearCart}
              className="text-rose-400 hover:text-rose-300 transition text-xs font-semibold"
            >
              Clear Entire Cart
            </button>
          </div>
        </div>

        {/* Order Summary Sidebar */}
        <div className="space-y-6">
          <div className="bg-slate-800/80 border border-slate-700/80 rounded-2xl p-6 shadow-xl">
            <h2 className="text-lg font-bold text-white mb-4">Order Summary</h2>

            <div className="space-y-3 text-sm">
              <div className="flex justify-between text-slate-300">
                <span>Subtotal</span>
                <span className="font-semibold text-white">
                  ₹{subTotal.toLocaleString('en-IN', { minimumFractionDigits: 2 })}
                </span>
              </div>

              {estimatedDiscount > 0 ? (
                <div className="flex justify-between text-emerald-400">
                  <span className="flex items-center gap-1">
                    <span>5% Volume Discount</span>
                  </span>
                  <span className="font-semibold">
                    -₹{estimatedDiscount.toLocaleString('en-IN', { minimumFractionDigits: 2 })}
                  </span>
                </div>
              ) : (
                <div className="text-xs text-slate-400 bg-slate-900/50 p-2.5 rounded-lg border border-slate-700/50">
                  💡 Spend over ₹10,000 to unlock an automatic 5% server discount!
                </div>
              )}

              <div className="text-xs text-indigo-400/80 bg-indigo-950/30 p-2.5 rounded-lg border border-indigo-900/40">
                🎉 Customer loyalty: Every 5th order receives an extra 10% discount calculated at checkout!
              </div>

              <div className="border-t border-slate-700 pt-3 flex justify-between text-base font-bold text-white">
                <span>Estimated Total</span>
                <span className="text-xl text-emerald-400">
                  ₹{estimatedTotal.toLocaleString('en-IN', { minimumFractionDigits: 2 })}
                </span>
              </div>
            </div>

            <button
              onClick={handleCheckout}
              disabled={isSubmitting}
              className="mt-6 w-full py-3.5 px-4 bg-gradient-to-r from-indigo-600 to-indigo-500 hover:from-indigo-500 hover:to-indigo-400 disabled:opacity-50 text-white font-semibold rounded-xl shadow-lg shadow-indigo-600/30 transition flex items-center justify-center gap-2"
            >
              {isSubmitting ? (
                <div className="w-5 h-5 border-2 border-white/30 border-t-white rounded-full animate-spin" />
              ) : (
                <>
                  <ShieldCheck size={18} />
                  <span>Place Order</span>
                </>
              )}
            </button>

            <div className="mt-4 flex items-center justify-center gap-2 text-xs text-slate-400">
              <AlertCircle size={14} className="text-indigo-400" />
              <span>Idempotency-Key enabled: safe against double clicks</span>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};

export default CartPage;
