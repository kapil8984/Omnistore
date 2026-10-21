import * as signalR from '@microsoft/signalr';

const HUB_URL = (import.meta as any).env?.VITE_HUB_URL || 'http://localhost:5069/hubs/orders';

let connection: signalR.HubConnection | null = null;
const statusListeners: Array<(orderId: number, newStatus: any) => void> = [];

export const getSignalRConnection = (): signalR.HubConnection => {
  if (!connection) {
    connection = new signalR.HubConnectionBuilder()
      .withUrl(HUB_URL, {
        accessTokenFactory: () => localStorage.getItem('accessToken') || '',
      })
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    connection.on('OrderStatusChanged', (arg1: any, arg2?: any) => {
      const orderId = typeof arg1 === 'object' && arg1.orderId !== undefined ? arg1.orderId : arg1;
      const status = typeof arg1 === 'object' && arg1.status !== undefined ? arg1.status : arg2;
      statusListeners.forEach((listener) => listener(orderId, status));
    });
  }
  return connection;
};

export const startSignalRConnection = async (
  userId?: number,
  isAdmin?: boolean,
  onStatusChanged?: (data: { orderId: number; status: string; customerId: number }) => void,
  onOrderCreated?: (order: unknown) => void
): Promise<signalR.HubConnection | null> => {
  const conn = getSignalRConnection();
  if (conn.state === signalR.HubConnectionState.Connected) {
    return conn;
  }

  if (onStatusChanged) {
    conn.on('OrderStatusChanged', onStatusChanged);
  }

  if (onOrderCreated) {
    conn.on('OrderCreated', onOrderCreated);
  }

  try {
    await conn.start();
    if (userId) {
      await conn.invoke('JoinUserGroup', userId.toString());
    }
    if (isAdmin) {
      await conn.invoke('JoinAdminGroup');
    }
    return conn;
  } catch (error) {
    console.warn('SignalR connection could not be established:', error);
    return null;
  }
};

export const stopSignalRConnection = async () => {
  if (connection) {
    try {
      await connection.stop();
    } catch {
      // Ignore cleanup error
    }
    connection = null;
  }
};

export const signalrService = {
  startConnection: startSignalRConnection,
  stopConnection: stopSignalRConnection,
  onOrderStatusChanged: (callback: (orderId: number, status: any) => void) => {
    statusListeners.push(callback);
    const conn = getSignalRConnection();
    conn.on('OrderStatusChanged', (arg1: any, arg2?: any) => {
      const orderId = typeof arg1 === 'object' && arg1.orderId !== undefined ? arg1.orderId : arg1;
      const status = typeof arg1 === 'object' && arg1.status !== undefined ? arg1.status : arg2;
      callback(orderId, status);
    });
  },
  offOrderStatusChanged: (callback: (orderId: number, status: any) => void) => {
    const idx = statusListeners.indexOf(callback);
    if (idx > -1) {
      statusListeners.splice(idx, 1);
    }
  },
};

export default signalrService;
