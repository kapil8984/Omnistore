import React, { useState } from 'react';
import { Link, useNavigate, useLocation } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import { useCart } from '../context/CartContext';
import { ShoppingCart, Package, Shield, LogOut, LogIn, Menu, X, Radio } from 'lucide-react';

export const Navbar: React.FC = () => {
  const { user, isAuthenticated, isAdmin, logout } = useAuth();
  const { itemCount } = useCart();
  const navigate = useNavigate();
  const location = useLocation();
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);

  const isActive = (path: string) => location.pathname === path;

  return (
    <header className="sticky top-0 z-50 bg-slate-900/80 backdrop-blur-xl border-b border-slate-800/80">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <div className="flex items-center justify-between h-16">
          {/* Logo / Brand */}
          <Link to="/" className="flex items-center gap-2 group">
            <div className="w-10 h-10 rounded-xl bg-gradient-to-tr from-emerald-600 to-teal-400 flex items-center justify-center shadow-lg shadow-emerald-500/20 group-hover:scale-105 transition-transform">
              <Package className="w-5 h-5 text-white" />
            </div>
            <div>
              <span className="text-xl font-bold bg-gradient-to-r from-white to-slate-400 bg-clip-text text-transparent">
                OmniStore
              </span>
              <span className="hidden sm:inline-block text-[10px] uppercase tracking-wider font-semibold px-2 py-0.5 ml-2 rounded-full bg-emerald-500/10 text-emerald-400 border border-emerald-500/20">
                v1.0
              </span>
            </div>
          </Link>

          {/* Desktop Nav */}
          <nav className="hidden md:flex items-center gap-1">
            <Link
              to="/"
              className={`px-4 py-2 rounded-lg text-sm font-medium transition-colors ${
                isActive('/') ? 'bg-slate-800 text-emerald-400' : 'text-slate-300 hover:text-white hover:bg-slate-800/50'
              }`}
            >
              Catalog
            </Link>

            {isAuthenticated && (
              <Link
                to="/orders"
                className={`px-4 py-2 rounded-lg text-sm font-medium transition-colors ${
                  isActive('/orders') ? 'bg-slate-800 text-emerald-400' : 'text-slate-300 hover:text-white hover:bg-slate-800/50'
                }`}
              >
                My Orders
              </Link>
            )}

            {isAdmin && (
              <Link
                to="/admin"
                className={`flex items-center gap-1.5 px-4 py-2 rounded-lg text-sm font-medium transition-colors ${
                  isActive('/admin') ? 'bg-purple-950/60 text-purple-300 border border-purple-500/30' : 'text-purple-300 hover:bg-purple-900/30'
                }`}
              >
                <Shield className="w-4 h-4 text-purple-400" />
                Admin Portal
              </Link>
            )}
          </nav>

          {/* Right Action Icons */}
          <div className="hidden md:flex items-center gap-3">
            {/* Live Indicator */}
            <div className="flex items-center gap-1.5 px-2.5 py-1 rounded-full bg-slate-800/80 border border-slate-700/50 text-xs text-slate-400">
              <span className="w-2 h-2 rounded-full bg-emerald-500 animate-pulse"></span>
              <span className="text-[11px] font-medium text-slate-300">Live Sync</span>
            </div>

            {/* Cart Button */}
            <Link
              to="/cart"
              className="relative p-2.5 rounded-xl bg-slate-800/80 hover:bg-slate-700/80 border border-slate-700/50 text-slate-200 transition-colors"
              aria-label="Shopping Cart"
            >
              <ShoppingCart className="w-5 h-5" />
              {itemCount > 0 && (
                <span className="absolute -top-1.5 -right-1.5 bg-emerald-500 text-slate-950 font-bold text-[11px] w-5 h-5 rounded-full flex items-center justify-center shadow-lg shadow-emerald-500/30 animate-in zoom-in">
                  {itemCount}
                </span>
              )}
            </Link>

            {/* User Profile or Login */}
            {isAuthenticated ? (
              <div className="flex items-center gap-3 pl-2 border-l border-slate-800">
                <div className="text-right">
                  <div className="text-sm font-semibold text-slate-200">{user?.fullName}</div>
                  <div className="text-xs text-slate-400 flex items-center justify-end gap-1">
                    <span className={`inline-block w-1.5 h-1.5 rounded-full ${isAdmin ? 'bg-purple-400' : 'bg-emerald-400'}`}></span>
                    {user?.role}
                  </div>
                </div>

                <button
                  onClick={() => {
                    logout();
                    navigate('/login');
                  }}
                  className="p-2 rounded-xl text-slate-400 hover:text-red-400 hover:bg-red-500/10 transition-colors"
                  title="Logout"
                  aria-label="Logout"
                >
                  <LogOut className="w-5 h-5" />
                </button>
              </div>
            ) : (
              <Link
                to="/login"
                className="flex items-center gap-2 px-4 py-2 rounded-xl bg-emerald-600 hover:bg-emerald-500 text-white font-medium text-sm shadow-lg shadow-emerald-600/20 transition-all hover:scale-[1.02]"
              >
                <LogIn className="w-4 h-4" />
                Sign In
              </Link>
            )}
          </div>

          {/* Mobile menu button */}
          <div className="flex md:hidden items-center gap-2">
            <Link
              to="/cart"
              className="relative p-2 rounded-lg bg-slate-800 text-slate-200"
            >
              <ShoppingCart className="w-5 h-5" />
              {itemCount > 0 && (
                <span className="absolute -top-1 -right-1 bg-emerald-500 text-slate-950 font-bold text-[10px] w-4 h-4 rounded-full flex items-center justify-center">
                  {itemCount}
                </span>
              )}
            </Link>

            <button
              onClick={() => setMobileMenuOpen(!mobileMenuOpen)}
              className="p-2 rounded-lg bg-slate-800 text-slate-200"
              aria-label="Toggle Navigation"
            >
              {mobileMenuOpen ? <X className="w-6 h-6" /> : <Menu className="w-6 h-6" />}
            </button>
          </div>
        </div>
      </div>

      {/* Mobile Menu Dropdown */}
      {mobileMenuOpen && (
        <div className="md:hidden border-b border-slate-800 bg-slate-900/95 backdrop-blur-xl px-4 pt-2 pb-4 space-y-2">
          <Link
            to="/"
            onClick={() => setMobileMenuOpen(false)}
            className="block px-3 py-2 rounded-lg text-base font-medium text-slate-300 hover:bg-slate-800"
          >
            Catalog
          </Link>
          {isAuthenticated && (
            <Link
              to="/orders"
              onClick={() => setMobileMenuOpen(false)}
              className="block px-3 py-2 rounded-lg text-base font-medium text-slate-300 hover:bg-slate-800"
            >
              My Orders
            </Link>
          )}
          {isAdmin && (
            <Link
              to="/admin"
              onClick={() => setMobileMenuOpen(false)}
              className="block px-3 py-2 rounded-lg text-base font-medium text-purple-300 hover:bg-purple-950/40"
            >
              Admin Dashboard
            </Link>
          )}
          <div className="pt-2 border-t border-slate-800">
            {isAuthenticated ? (
              <div className="flex items-center justify-between py-2">
                <div>
                  <div className="font-semibold text-slate-200">{user?.fullName}</div>
                  <div className="text-xs text-slate-400">{user?.email} ({user?.role})</div>
                </div>
                <button
                  onClick={() => {
                    logout();
                    setMobileMenuOpen(false);
                    navigate('/login');
                  }}
                  className="px-3 py-1.5 rounded-lg bg-red-500/20 text-red-300 text-sm font-medium"
                >
                  Sign Out
                </button>
              </div>
            ) : (
              <Link
                to="/login"
                onClick={() => setMobileMenuOpen(false)}
                className="block text-center w-full py-2.5 rounded-xl bg-emerald-600 text-white font-medium text-sm"
              >
                Sign In
              </Link>
            )}
          </div>
        </div>
      )}
    </header>
  );
};

export default Navbar;

