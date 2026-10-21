import { describe, it, expect, beforeEach } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import React from 'react';
import { CartProvider, useCart } from '../context/CartContext';
import { Product } from '../types';

describe('CartContext', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  const sampleProduct: Product = {
    id: 1,
    sku: 'TEST-001',
    name: 'Ergonomic Desk',
    price: 6000,
    stockQuantity: 5,
    isActive: true,
  };

  const expensiveProduct: Product = {
    id: 2,
    sku: 'TEST-002',
    name: 'MacBook Pro',
    price: 15000,
    stockQuantity: 3,
    isActive: true,
  };

  it('initializes with empty cart', () => {
    const wrapper = ({ children }: { children: React.ReactNode }) => (
      <CartProvider>{children}</CartProvider>
    );

    const { result } = renderHook(() => useCart(), { wrapper });

    expect(result.current.items).toEqual([]);
    expect(result.current.itemCount).toBe(0);
    expect(result.current.subTotal).toBe(0);
    expect(result.current.estimatedDiscount).toBe(0);
  });

  it('adds item to cart and calculates subtotal and item count', () => {
    const wrapper = ({ children }: { children: React.ReactNode }) => (
      <CartProvider>{children}</CartProvider>
    );

    const { result } = renderHook(() => useCart(), { wrapper });

    act(() => {
      result.current.addItem(sampleProduct, 2);
    });

    expect(result.current.itemCount).toBe(2);
    expect(result.current.subTotal).toBe(12000);
    // Subtotal > 10,000 should give 5% discount (12000 * 0.05 = 600)
    expect(result.current.estimatedDiscount).toBe(600);
    expect(result.current.estimatedTotal).toBe(11400);
  });

  it('caps quantity to maximum available stock', () => {
    const wrapper = ({ children }: { children: React.ReactNode }) => (
      <CartProvider>{children}</CartProvider>
    );

    const { result } = renderHook(() => useCart(), { wrapper });

    act(() => {
      // Stock is 5, try adding 10
      result.current.addItem(sampleProduct, 10);
    });

    expect(result.current.items[0].quantity).toBe(5);
    expect(result.current.itemCount).toBe(5);
  });

  it('removes item and clears cart', () => {
    const wrapper = ({ children }: { children: React.ReactNode }) => (
      <CartProvider>{children}</CartProvider>
    );

    const { result } = renderHook(() => useCart(), { wrapper });

    act(() => {
      result.current.addItem(sampleProduct, 1);
      result.current.addItem(expensiveProduct, 1);
    });

    expect(result.current.items.length).toBe(2);

    act(() => {
      result.current.removeItem(sampleProduct.id);
    });

    expect(result.current.items.length).toBe(1);
    expect(result.current.items[0].product.id).toBe(expensiveProduct.id);

    act(() => {
      result.current.clearCart();
    });

    expect(result.current.items.length).toBe(0);
    expect(result.current.subTotal).toBe(0);
  });
});
