import { http } from '@/services/http/httpService';
import { crud } from '@/services/http/crud';
import type { Audited } from '@/types/api';

export interface DashboardItem {
  widget: string;
  title: string;
  componentType: string | null;
  x: number;
  y: number;
  w: number;
  h: number;
}

export interface DashboardView {
  source: string; layoutId: number | null; items: DashboardItem[];
  sourceScope?: DashboardScopeValue | null; sourceScopeId?: number | null; sourceCompanyId?: number | null; hiddenCount?: number;
}

export const dashboardApi = {
  mine: () => http.get<DashboardView>('/api/Dashboard/mine'),
};

export const DashboardScope = { Application: 0, CompanyType: 1, Company: 2, User: 3, Tenant: 4, Role: 5, UserType: 6 } as const;
export type DashboardScopeValue = (typeof DashboardScope)[keyof typeof DashboardScope];

export interface DashboardWidgetRead extends Audited {
  code: string; title: string; description: string | null; componentTypeId: number | null; permissionId: number | null;
  moduleCode: string | null; defaultWidth: number; defaultHeight: number;
}

export interface DashboardLayoutRead extends Audited {
  name: string; scope: DashboardScopeValue; scopeId: number; applicationTypeId: number | null; companyId: number | null; layoutJson: string; tenantId: number | null;
}
export interface DashboardLayoutWriteBody {
  name: string; scope: DashboardScopeValue; scopeId: number; applicationTypeId?: number | null; companyId?: number | null; layoutJson: string; isActive?: boolean;
}
export interface Placement { widget: string; x: number; y: number; w: number; h: number }

export const dashboardAdminApi = {
  preview: (query: { applicationTypeId: number; companyId?: number | null; roleId?: number | null; userType?: number | null }) =>
    http.get<DashboardView>('/api/DashboardStudio/preview', query),
  widgets: crud<DashboardWidgetRead, Partial<DashboardWidgetRead>>('DashboardWidget'),
  layouts: crud<DashboardLayoutRead, DashboardLayoutWriteBody>('DashboardLayout'),
};
