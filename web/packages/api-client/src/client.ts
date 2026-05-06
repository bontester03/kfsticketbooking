import axios, { AxiosError, AxiosInstance, InternalAxiosRequestConfig } from 'axios';
import { useAuthStore } from './auth-store';
import type { ApiError, AuthResponse } from '@kfs/types';

interface ClientOptions {
  baseURL?: string;
}

let refreshing: Promise<string> | null = null;

export function createApiClient({ baseURL = '/api/v1' }: ClientOptions = {}): AxiosInstance {
  const client = axios.create({ baseURL, timeout: 15000 });

  client.interceptors.request.use((config: InternalAxiosRequestConfig) => {
    const token = useAuthStore.getState().accessToken;
    if (token) config.headers.Authorization = `Bearer ${token}`;
    return config;
  });

  client.interceptors.response.use(
    (resp) => resp,
    async (error: AxiosError<ApiError>) => {
      const original = error.config as InternalAxiosRequestConfig & { _retry?: boolean };
      const status = error.response?.status;

      if (status === 401 && !original._retry && original.url !== '/auth/refresh') {
        original._retry = true;
        try {
          const newToken = await refreshAccessToken(client);
          if (newToken && original.headers) {
            original.headers.Authorization = `Bearer ${newToken}`;
            return client.request(original);
          }
        } catch {
          useAuthStore.getState().clear();
        }
      }

      return Promise.reject(toApiError(error));
    }
  );

  return client;
}

async function refreshAccessToken(client: AxiosInstance): Promise<string | null> {
  // Single-flight: concurrent 401s should share one refresh round-trip.
  if (!refreshing) {
    const refreshToken = useAuthStore.getState().refreshToken;
    if (!refreshToken) return null;
    refreshing = client
      .post<AuthResponse>('/auth/refresh', { refreshToken })
      .then((r) => {
        useAuthStore.getState().setAuth(r.data);
        return r.data.accessToken;
      })
      .finally(() => { refreshing = null; });
  }
  return refreshing;
}

function toApiError(error: AxiosError<ApiError>): ApiError {
  if (error.response?.data) return error.response.data;
  return {
    status: error.response?.status ?? 0,
    code: 'network_error',
    message: error.message
  };
}
