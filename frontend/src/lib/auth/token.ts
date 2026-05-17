let accessToken: string | null = null;
let expiresAt: number | null = null;

const STORAGE_KEY = 'rd.access';

interface StoredToken {
  token: string;
  expiresAt: number;
}

export function setAccessToken(token: string, isoExpires: string): void {
  accessToken = token;
  expiresAt = Date.parse(isoExpires);
  const payload: StoredToken = { token, expiresAt };
  sessionStorage.setItem(STORAGE_KEY, JSON.stringify(payload));
}

export function getAccessToken(): string | null {
  if (accessToken) return accessToken;
  const raw = sessionStorage.getItem(STORAGE_KEY);
  if (!raw) return null;
  try {
    const p = JSON.parse(raw) as StoredToken;
    accessToken = p.token;
    expiresAt = p.expiresAt;
    return accessToken;
  } catch {
    return null;
  }
}

export function clearAccessToken(): void {
  accessToken = null;
  expiresAt = null;
  sessionStorage.removeItem(STORAGE_KEY);
}

export function isExpiringSoon(thresholdMs = 30_000): boolean {
  if (!expiresAt) return false;
  return Date.now() + thresholdMs >= expiresAt;
}
