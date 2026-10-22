import { crud } from '@/services/http/crud';
import { http } from '@/services/http/httpService';
import type { Audited } from '@/types/api';

export interface RoleRead extends Audited {
  name: string;
  code: string | null;
  description: string | null;
  applicationTypeId: number | null;
  tenantId: number | null;
}

export interface RoleWrite {
  name: string;
  code?: string | null;
  description?: string | null;
  applicationTypeId?: number | null;
  isActive?: boolean;
}

export interface PermissionRead extends Audited {
  name: string;
  code: string;
  description: string | null;
  isManagement: boolean;
  managementRank: number | null;
}

export interface RolePermissionRead extends Audited { roleId: number; permissionId: number }

export interface ApplicationOption extends Audited { code: string; name: string }

export interface RoleCopyRequest { name: string; code?: string | null }

export const rolesApi = {
  copy: (id: number, body: RoleCopyRequest) => http.post<RoleRead>(`/api/Role/${id}/copy`, body),
  applications: crud<ApplicationOption, Partial<ApplicationOption>>('ApplicationType'),
  ...crud<RoleRead, RoleWrite>('Role'),
  permissions: crud<PermissionRead, Partial<PermissionRead>>('Permission'),
  grants: crud<RolePermissionRead, { roleId: number; permissionId: number }>('RolePermission'),
};
