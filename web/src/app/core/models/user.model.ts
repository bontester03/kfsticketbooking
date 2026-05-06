export type UserRole = 'Student' | 'Teacher' | 'Admin';

export interface AuthResponse {
  token: string;
  expiresAt: string;
  userId: string;
  email: string;
  fullName: string;
  role: UserRole;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest {
  fullName: string;
  email: string;
  password: string;
  role?: UserRole;
}

export interface CurrentUser {
  userId: string;
  email: string;
  fullName: string;
  role: UserRole;
}
