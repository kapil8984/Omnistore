import axios, { AxiosError, InternalAxiosRequestConfig } from 'axios';
import {
  AuthResponse,
  CreateOrderDto,
  CreateProductDto,
  Order,
  PagedResult,
  Product,
  ProductQueryParams,
  TopCustomerReport,
  UpdateProductDto,
  OrderStatus
} from '../types';

const API_BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:5000/api';

const api = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json',
  },
});

let isRefreshing = false;
let failedQueue: Array<{
  resolve: (value?: unknown) => void;
  reject: (reason?: unknown) => void;
}> = [];

const processQueue = (error: unknown, token: string | null = null) => {
  failedQueue.forEach((prom) => {
    if (error) {
      prom.reject(error);
    } else {
      prom.resolve(token);
    }
  });
  failedQueue = [];
};

// Request Interceptor: Attach JWT token
api.interceptors.request.use(
  (config: InternalAxiosRequestConfig) => {
    const token = localStorage.getItem('accessToken');
    if (token && config.headers) {
      config.headers.Authorization = `Bearer ${token}`;
    }
    return config;
  },
  (error) => Promise.reject(error)
);

// Response Interceptor: 401 refresh & retry once, 409 and 429 handling
api.interceptors.response.use(
  (response) => response,
  async (error: AxiosError<{ title?: string; detail?: string }>) => {
    const originalRequest = error.config as InternalAxiosRequestConfig & { _retry?: boolean };

    // Handle 429 Rate Limiting
    if (error.response?.status === 429) {
      const message = error.response.data?.detail || 'Too many attempts. Please wait 1 minute before trying again.';
      return Promise.reject(new Error(message));
    }

    // Handle 409 Conflict
    if (error.response?.status === 409) {
      const message = error.response.data?.detail || 'A concurrency conflict occurred or the item is out of stock.';
      return Promise.reject(new Error(message));
    }

    // Handle 401 Unauthorized with Automatic Refresh Token Flow (Retries original request ONCE)
    if (error.response?.status === 401 && originalRequest && !originalRequest._retry) {
      // If the 401 happened on login or refresh endpoint itself, don't attempt to refresh
      if (originalRequest.url?.includes('/auth/login') || originalRequest.url?.includes('/auth/refresh')) {
        return Promise.reject(error);
      }

      if (isRefreshing) {
        return new Promise((resolve, reject) => {
          failedQueue.push({ resolve, reject });
        })
          .then((token) => {
            if (originalRequest.headers) {
              originalRequest.headers.Authorization = `Bearer ${token}`;
            }
            return api(originalRequest);
          })
          .catch((err) => Promise.reject(err));
      }

      originalRequest._retry = true;
      isRefreshing = true;

      const accessToken = localStorage.getItem('accessToken');
      const refreshToken = localStorage.getItem('refreshToken');

      if (!refreshToken || !accessToken) {
        localStorage.removeItem('accessToken');
        localStorage.removeItem('refreshToken');
        localStorage.removeItem('user');
        window.location.href = '/login';
        return Promise.reject(error);
      }

      try {
        const { data } = await axios.post<AuthResponse>(`${API_BASE_URL}/auth/refresh`, {
          accessToken,
          refreshToken,
        });

        localStorage.setItem('accessToken', data.accessToken);
        localStorage.setItem('refreshToken', data.refreshToken);
        localStorage.setItem('user', JSON.stringify(data.user));

        api.defaults.headers.common['Authorization'] = `Bearer ${data.accessToken}`;
        if (originalRequest.headers) {
          originalRequest.headers.Authorization = `Bearer ${data.accessToken}`;
        }

        processQueue(null, data.accessToken);
        return api(originalRequest);
      } catch (refreshError) {
        processQueue(refreshError, null);
        localStorage.removeItem('accessToken');
        localStorage.removeItem('refreshToken');
        localStorage.removeItem('user');
        window.location.href = '/login';
        return Promise.reject(refreshError);
      } finally {
        isRefreshing = false;
      }
    }

    const errorMsg = error.response?.data?.detail || error.response?.data?.title || error.message;
    return Promise.reject(new Error(errorMsg));
  }
);

// Typed API Service Methods
export const authApi = {
  login: async (email: string, password: string): Promise<AuthResponse> => {
    const res = await api.post<AuthResponse>('/auth/login', { email, password });
    return res.data;
  },
  register: async (fullName: string, email: string, password: string): Promise<AuthResponse> => {
    const res = await api.post<AuthResponse>('/auth/register', { fullName, email, password, role: 'Customer' });
    return res.data;
  },
};

export const productsApi = {
  getProducts: async (params: ProductQueryParams): Promise<PagedResult<Product>> => {
    const res = await api.get<PagedResult<Product>>('/products', { params });
    return res.data;
  },
  getProductById: async (id: number): Promise<Product> => {
    const res = await api.get<Product>(`/products/${id}`);
    return res.data;
  },
  createProduct: async (dto: CreateProductDto): Promise<Product> => {
    const res = await api.post<Product>('/products', dto);
    return res.data;
  },
  updateProduct: async (id: number, dto: UpdateProductDto): Promise<Product> => {
    const res = await api.put<Product>(`/products/${id}`, dto);
    return res.data;
  },
};

export const ordersApi = {
  createOrder: async (dto: CreateOrderDto, idempotencyKey: string): Promise<Order> => {
    const res = await api.post<Order>('/orders', dto, {
      headers: {
        'Idempotency-Key': idempotencyKey,
      },
    });
    return res.data;
  },
  getMyOrders: async (page = 1, pageSize = 10): Promise<PagedResult<Order>> => {
    const res = await api.get<PagedResult<Order>>('/orders/my', {
      params: { page, pageSize },
    });
    return res.data;
  },
  getOrderById: async (id: number): Promise<Order> => {
    const res = await api.get<Order>(`/orders/${id}`);
    return res.data;
  },
  updateStatus: async (id: number, status: OrderStatus): Promise<Order> => {
    const res = await api.patch<Order>(`/orders/${id}/status`, { status });
    return res.data;
  },
};

export const reportsApi = {
  getTopCustomers: async (days = 30): Promise<TopCustomerReport[]> => {
    const res = await api.get<TopCustomerReport[]>('/reports/top-customers', {
      params: { days },
    });
    return res.data;
  },
};

export default api;
