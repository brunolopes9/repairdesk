export interface UserInfo {
  id: string;
  email: string;
  displayName: string;
  tenantId: string;
  roles: string[];
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
