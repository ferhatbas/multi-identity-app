import { http } from '@/services/http/httpService';
import { crud } from '@/services/http/crud';
import type { Audited } from '@/types/api';

export interface UnitTypeRead extends Audited { code: string; name: string }

export interface UnitRead extends Audited {
  code: string;
  name: string;
  description: string | null;
  parentId: number | null;
  unitTypeId: number;
  managerUserId: number | null;
  companyId: number | null;
}

export interface UnitWrite {
  code: string;
  name: string;
  description?: string | null;
  parentId?: number | null;
  unitTypeId: number;
  managerUserId?: number | null;
  isActive?: boolean;
}

export interface UnitNode {
  id: number;
  code: string;
  name: string;
  unitTypeId: number;
  managerUserId: number | null;
  children: UnitNode[];
}

export interface UserUnitRead extends Audited { userId: number; unitId: number; isPrimary: boolean }

export interface OrganizationUnit {
  id: number; parentId: number | null; companyId: number | null; code: string; name: string; unitTypeId: number;
  managerUserId: number | null; description: string | null; isActive: boolean;
}

export const unitsApi = {
  organization: () => http.get<OrganizationUnit[]>('/api/Unit/organization'),
  ...crud<UnitRead, UnitWrite>('Unit'),
  tree: () => http.get<UnitNode[]>('/api/Unit/tree'),
  types: crud<UnitTypeRead, { code: string; name: string; isActive?: boolean }>('UnitType'),
  members: crud<UserUnitRead, { userId: number; unitId: number; isPrimary: boolean }>('UserUnit'),
};
