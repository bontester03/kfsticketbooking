import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import type { AuthResponse } from '@kfs/types';

// Per spec section 15.5 the access token should live in memory and the refresh token in
// an HttpOnly cookie. The current backend returns the refresh token in the JSON body, so
// for now we persist both — and document the cookie migration as a follow-up in
// DECISIONS.md (requires backend changes too).

export interface AuthState {
  accessToken: string | null;
  accessExpiresAt: string | null;
  refreshToken: string | null;
  refreshExpiresAt: string | null;
  userId: string | null;
  email: string | null;
  displayName: string | null;
  mustChangePassword: boolean;
  setAuth: (resp: AuthResponse) => void;
  clear: () => void;
  isAuthenticated: () => boolean;
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set, get) => ({
      accessToken: null,
      accessExpiresAt: null,
      refreshToken: null,
      refreshExpiresAt: null,
      userId: null,
      email: null,
      displayName: null,
      mustChangePassword: false,
      setAuth: (resp) => set({
        accessToken: resp.accessToken,
        accessExpiresAt: resp.accessExpiresAt,
        refreshToken: resp.refreshToken,
        refreshExpiresAt: resp.refreshExpiresAt,
        userId: resp.userId,
        email: resp.email,
        displayName: resp.displayName,
        mustChangePassword: resp.mustChangePassword
      }),
      clear: () => set({
        accessToken: null, accessExpiresAt: null,
        refreshToken: null, refreshExpiresAt: null,
        userId: null, email: null, displayName: null,
        mustChangePassword: false
      }),
      isAuthenticated: () => {
        const { accessToken, accessExpiresAt } = get();
        if (!accessToken || !accessExpiresAt) return false;
        return new Date(accessExpiresAt).getTime() > Date.now();
      }
    }),
    { name: 'kfs.auth' }
  )
);
