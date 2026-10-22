const ACCESS = 'fsi.access';
const REFRESH = 'fsi.refresh';

function read(key: string): string | null {
  try { return localStorage.getItem(key); } catch { return null; }
}

function write(key: string, value: string | null | undefined): void {
  try {
    if (value) localStorage.setItem(key, value);
    else localStorage.removeItem(key);
  } catch { /* storage may be blocked; the session then lasts until reload */ }
}

export const getAccessToken = () => read(ACCESS);
export const getRefreshToken = () => read(REFRESH);

export function setTokens(access: string, refresh?: string | null): void {
  write(ACCESS, access);
  if (refresh) write(REFRESH, refresh);
}

export function clearTokens(): void {
  write(ACCESS, null);
  write(REFRESH, null);
}

export function accessTokenExpiry(token = getAccessToken()): number | null {
  if (!token) return null;
  try {
    const payload = token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
    const exp = JSON.parse(atob(payload)).exp;
    return typeof exp === 'number' ? exp : null;
  } catch {
    return null;
  }
}
