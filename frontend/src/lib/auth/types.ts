export interface UserInfo {
  id: string;
  email: string;
  displayName: string;
  tenantId: string;
  roles: string[];
  requireChangePasswordOnNextLogin: boolean;
  /** Sprint 420: telefone do utilizador (opcional). */
  phoneNumber: string | null;
}

/** Sprint 420: payload PUT /api/auth/me. */
export interface UpdateMeRequest {
  displayName: string;
  phoneNumber: string | null;
}

export interface AuthResponse {
  accessToken: string;
  accessTokenExpiresAt: string;
  user: UserInfo;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
}
