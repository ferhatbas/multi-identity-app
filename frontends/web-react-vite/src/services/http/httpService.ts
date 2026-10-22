import type { ApiResponse, QueryValue } from '@/types/api';
import type { AuthResponse } from '@/types/auth';
import { accessTokenExpiry, clearTokens, getAccessToken, getRefreshToken, setTokens } from '@/features/auth/session';

const BASE = import.meta.env.VITE_API_BASE_URL ?? '';

const MARGIN_SECONDS = 30;

const PUBLIC_PATHS = [
  '/api/Auth/login', '/api/Auth/login/2fa', '/api/Auth/refresh',
  '/api/Auth/forgot-password', '/api/Auth/reset-password', '/api/Auth/confirm-email',
];

export class ApiException extends Error {
  constructor(
    message: string,
    public status: number,
    public errors: { code: string; message: string; field: string | null }[] = [],
  ) {
    super(message);
  }

  get code(): string | undefined {
    return this.errors[0]?.code;
  }

  fieldMessage(field: string): string | undefined {
    return this.errors.find(e => e.field?.toLowerCase() === field.toLowerCase())?.message;
  }
}

type Listener = () => void;
const signedOutListeners = new Set<Listener>();

export function onSignedOut(listener: Listener): () => void {
  signedOutListeners.add(listener);
  return () => { signedOutListeners.delete(listener); };
}

let refreshing: Promise<boolean> | null = null;

function refreshOnce(): Promise<boolean> {
  refreshing ??= (async () => {
    const refreshToken = getRefreshToken();
    if (!refreshToken) return false;
    try {
      const response = await fetch(`${BASE}/api/Auth/refresh`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ refreshToken }),
      });
      if (!response.ok) return false;
      const body = (await response.json()) as ApiResponse<AuthResponse>;
      setTokens(body.data.accessToken, body.data.refreshToken);
      return true;
    } catch {
      return false;
    } finally {
      setTimeout(() => { refreshing = null; }, 0);
    }
  })();
  return refreshing;
}

async function freshToken(path: string): Promise<string | null> {
  const token = getAccessToken();
  if (!token || PUBLIC_PATHS.includes(path)) return token;
  const exp = accessTokenExpiry(token);
  if (exp !== null && exp - MARGIN_SECONDS <= Date.now() / 1000) await refreshOnce();
  return getAccessToken();
}

function toQuery(query?: Record<string, QueryValue>): string {
  if (!query) return '';
  const parts = Object.entries(query)
    .filter(([, v]) => v !== undefined && v !== null && v !== '')
    .map(([k, v]) => `${encodeURIComponent(k)}=${encodeURIComponent(String(v))}`);
  return parts.length ? `?${parts.join('&')}` : '';
}

async function send<T>(method: string, path: string, body?: unknown, query?: Record<string, QueryValue>, retried = false): Promise<T> {
  const token = await freshToken(path);
  const headers: Record<string, string> = {};
  if (body !== undefined) headers['Content-Type'] = 'application/json';
  if (token) headers.Authorization = `Bearer ${token}`;

  let response: Response;
  try {
    response = await fetch(`${BASE}${path}${toQuery(query)}`, {
      method,
      headers,
      body: body === undefined ? undefined : JSON.stringify(body),
    });
  } catch {
    throw new ApiException('The server cannot be reached', 0);
  }

  if (response.status === 401 && !retried && !PUBLIC_PATHS.includes(path)) {
    if (await refreshOnce()) return send<T>(method, path, body, query, true);
    clearTokens();
    signedOutListeners.forEach(l => l());
  }

  let envelope: ApiResponse<T> | null = null;
  try {
    envelope = (await response.json()) as ApiResponse<T>;
  } catch { /* an empty or non-JSON body */ }

  if (!response.ok) {
    const text = envelope?.message || envelope?.errors?.map(e => e.message).join(' · ') || `Request failed (${response.status})`;
    throw new ApiException(text, response.status, envelope?.errors ?? []);
  }
  return envelope?.data as T;
}

export const http = {
  get: <T>(path: string, query?: Record<string, QueryValue>) => send<T>('GET', path, undefined, query),
  post: <T>(path: string, body?: unknown) => send<T>('POST', path, body ?? {}),
  put: <T>(path: string, body?: unknown) => send<T>('PUT', path, body ?? {}),
  delete: <T>(path: string) => send<T>('DELETE', path),
};
