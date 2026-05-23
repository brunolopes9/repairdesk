export interface UserInfo {
  id: string;
  email: string;
  displayName: string;
  tenantId: string;
  roles: string[];
  requireChangePasswordOnNextLogin: boolean;
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
