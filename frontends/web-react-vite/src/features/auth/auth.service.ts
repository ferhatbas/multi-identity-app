import { http } from '@/services/http/httpService';
import type { AuthResponse, MeResponse } from '@/types/auth';

export interface TwoFactorSetup { secret: string; otpAuthUri: string }
export interface RecoveryCodes { codes: string[] }

export interface SessionRead {
  id: number;
  ipAddress: string | null;
  userAgent: string | null;
  createdAt: string;
  lastSeenAt: string | null;
  expiresAt: string;
  revokedAt: string | null;
  impersonatorUserId: number | null;
  isCurrent: boolean;
  isOpen: boolean;
}

export interface MyLanguage { languageCode: string | null; effectiveLanguageCode: string }

export const authApi = {
  setLanguage: (languageCode: string | null) => http.put<MyLanguage>('/api/Auth/me/language', { languageCode }),
  login: (userName: string, password: string) => http.post<AuthResponse>('/api/Auth/login', { userName, password }),
  loginTwoFactor: (twoFactorToken: string, code: string) => http.post<AuthResponse>('/api/Auth/login/2fa', { twoFactorToken, code }),
  me: () => http.get<MeResponse>('/api/Auth/me'),
  logout: () => http.post<boolean>('/api/Auth/logout'),
  logoutAll: () => http.post<number>('/api/Auth/logout-all'),
  changeCompany: (companyId: number) => http.post<AuthResponse>('/api/Auth/change-company', { companyId }),
  changePassword: (currentPassword: string, newPassword: string) =>
    http.post<boolean>('/api/Auth/change-password', { currentPassword, newPassword }),
  forgotPassword: (email: string) => http.post<boolean>('/api/Auth/forgot-password', { email }),
  resetPassword: (token: string, newPassword: string) => http.post<boolean>('/api/Auth/reset-password', { token, newPassword }),
  confirmEmail: (token: string) => http.post<boolean>('/api/Auth/confirm-email', { token }),
  sendConfirmation: () => http.post<boolean>('/api/Auth/send-confirmation'),
  sessions: () => http.get<SessionRead[]>('/api/Auth/sessions'),
  revokeSession: (id: number) => http.post<boolean>(`/api/Auth/sessions/${id}/revoke`),
  twoFactorSetup: () => http.post<TwoFactorSetup>('/api/Auth/2fa/setup'),
  twoFactorEnable: (code: string) => http.post<RecoveryCodes>('/api/Auth/2fa/enable', { code }),
  twoFactorDisable: (password: string, code: string) => http.post<boolean>('/api/Auth/2fa/disable', { password, code }),
  newRecoveryCodes: (password: string, code: string) => http.post<RecoveryCodes>('/api/Auth/2fa/recovery-codes', { password, code }),
};
