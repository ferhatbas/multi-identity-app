import { http } from '@/services/http/httpService';
import { crud } from '@/services/http/crud';
import type { Audited } from '@/types/api';
import type { SessionRead } from '@/features/auth/auth.service';
import type { AuthResponse } from '@/types/auth';

export interface UserRead extends Audited {
  userName: string;
  email: string;
  firstName: string;
  lastName: string;
  phoneNumber: string | null;
  emailConfirmed: boolean;
  shouldChangePasswordOnNextLogin: boolean;
  activeCompanyId: number | null;
  languageCode: string | null;
  isPlatformAdmin: boolean;
  twoFactorEnabled: boolean;
  tenantId: number | null;
}

export interface UserWrite {
  userName: string;
  email: string;
  firstName: string;
  lastName: string;
  phoneNumber?: string | null;
  languageCode?: string | null;
  activeCompanyId?: number | null;
  isActive?: boolean;
  password?: string;
  shouldChangePasswordOnNextLogin?: boolean;
  tenantId?: number | null;
}

export interface EffectivePermission { code: string; name: string; via: string[] }
export interface EffectivePermissions { userId: number; companyId: number | null; granted: EffectivePermission[]; denied: string[] }
export interface TemporaryPassword { temporaryPassword: string }

export interface UserCompanyRead extends Audited { userId: number; companyId: number; membershipType: number }
export interface UserCompanyRoleRead extends Audited { userId: number; companyId: number; roleId: number }

export interface UserCopyRequest {
  userName: string; email: string; firstName?: string | null; lastName?: string | null; password?: string | null;
  copyMemberships: boolean; copyRoles: boolean; copyPermissions: boolean;
}
export interface CopiedUser { user: UserRead; temporaryPassword: string }

export interface UserPermissionOverride extends Audited { userId: number; companyId: number; permissionId: number; isGranted: boolean }

export interface BulkUserRow {
  userName: string; email: string; firstName: string; lastName: string; password?: string | null;
  companyId?: number | null; membershipType?: number; roleIds?: number[];
}
export interface BulkUserImportResult {
  created: number; rolledBack: boolean;
  users: { id: number; userName: string; temporaryPassword: string | null }[];
  failed: { row: number; userName: string; error: string }[];
}

export const usersApi = {
  ...crud<UserRead, UserWrite>('User'),
  resetPassword: (id: number) => http.post<TemporaryPassword>(`/api/User/${id}/reset-password`),
  unlock: (id: number) => http.post<boolean>(`/api/User/${id}/unlock`),
  sessions: (id: number) => http.get<SessionRead[]>(`/api/User/${id}/sessions`),
  revokeAllSessions: (id: number) => http.post<number>(`/api/User/${id}/sessions/revoke`),
  revokeSession: (id: number, sessionId: number) => http.post<boolean>(`/api/User/${id}/sessions/${sessionId}/revoke`),
  effectivePermissions: (id: number, companyId?: number) =>
    http.get<EffectivePermissions>(`/api/User/${id}/effective-permissions`, { companyId }),
  impersonate: (userId: number, reason?: string) => http.post<AuthResponse>('/api/Auth/impersonate', { userId, reason }),
  memberships: crud<UserCompanyRead, { userId: number; companyId: number; membershipType: number }>('UserCompany'),
  copy: (id: number, body: UserCopyRequest) => http.post<CopiedUser>(`/api/User/${id}/copy`, body),
  importUsers: (users: BulkUserRow[], atomic: boolean) => http.post<BulkUserImportResult>('/api/User/import', { users, atomic }),
  permissionOverrides: crud<UserPermissionOverride, { userId: number; companyId: number; permissionId: number; isGranted: boolean }>('UserCompanyPermission'),
  roleGrants: crud<UserCompanyRoleRead, { userId: number; companyId: number; roleId: number }>('UserCompanyRole'),
};
