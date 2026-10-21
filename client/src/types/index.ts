export type Role = 'Admin' | 'Customer';

export type OrderStatus = 'Pending' | 'Confirmed' | 'Shipped' | 'Cancelled';

export interface User {
  id: number;
  fullName: string;
  email: string;
  role: Role;
}

export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  expiresAt: string;
  user: User;
}

export interface Product {
  id: number;
  sku: string;
  name: string;
  price: number;
  stockQuantity: number;
  isActive: boolean;
}

export interface CreateProductDto {
  sku: string;
  name: string;
  price: number;
  stockQuantity: number;
  isActive: boolean;
}

export interface UpdateProductDto {
  sku: string;
  name: string;
  price: number;
  stockQuantity: number;
  isActive: boolean;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface ProductQueryParams {
  search?: string;
  minPrice?: number;
  maxPrice?: number;
  inStockOnly?: boolean;
  sortBy?: string;
  isDescending?: boolean;
  page?: number;
  pageSize?: number;
}

export interface CreateOrderItemDto {
  productId: number;
  quantity: number;
}

export interface CreateOrderDto {
  items: CreateOrderItemDto[];
}

export interface OrderItem {
  id: number;
  productId: number;
  productName: string;
  sku: string;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
}

export interface Order {
  id: number;
  customerId: number;
  customerName: string;
  customerEmail: string;
  status: OrderStatus;
  subTotal: number;
  discount: number;
  total: number;
  createdAt: string;
  items: OrderItem[];
}

export interface TopCustomerReport {
  customerId: number;
  customerName: string;
  email: string;
  totalSpend: number;
  orderCount: number;
  lastOrderDate?: string;
}

export interface CartItem {
  product: Product;
  quantity: number;
}
