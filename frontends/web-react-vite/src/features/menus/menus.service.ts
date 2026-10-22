import { crud } from '@/services/http/crud';
import { http } from '@/services/http/httpService';
import type { Audited } from '@/types/api';

export interface MenuRead extends Audited {
  parentId: number | null;
  code: string;
  title: string;
  path: string | null;
  icon: string | null;
  displayOrder: number;
  menuTypeId: number | null;
  applicationTypeId: number | null;
  permissionId: number | null;
  moduleCode: string | null;
  featureCode: string | null;
}

export interface MenuWrite {
  parentId?: number | null;
  code: string;
  title: string;
  path?: string | null;
  icon?: string | null;
  displayOrder: number;
  menuTypeId?: number | null;
  applicationTypeId?: number | null;
  permissionId?: number | null;
  moduleCode?: string | null;
  featureCode?: string | null;
  isActive?: boolean;
}

export interface MenuTypeRead extends Audited { code: string; name: string }

export const menusApi = {
  ...crud<MenuRead, MenuWrite>('Menu'),
  types: crud<MenuTypeRead, Partial<MenuTypeRead>>('MenuType'),
};

export interface MenuTreeNode { key: string; data: MenuRead; children: MenuTreeNode[] }

export function buildMenuTree(menus: MenuRead[]): MenuTreeNode[] {
  const nodes = new Map(menus.map(m => [m.id, { key: String(m.id), data: m, children: [] as MenuTreeNode[] }]));
  const roots: MenuTreeNode[] = [];
  for (const n of nodes.values()) {
    const parent = n.data.parentId ? nodes.get(n.data.parentId) : undefined;
    (parent ? parent.children : roots).push(n);
  }
  const sort = (list: MenuTreeNode[]) => { list.sort((a, b) => a.data.displayOrder - b.data.displayOrder || a.data.title.localeCompare(b.data.title)); list.forEach(n => sort(n.children)); };
  sort(roots);
  return roots;
}

export interface MenuCompanyTypeRead extends Audited { menuId: number; companyTypeId: number }
export interface MenuLocalizationRead extends Audited { menuId: number; languageCode: string; title: string }
export interface MenuLayoutRead extends Audited { menuId: number; isHidden: boolean; displayOrder: number | null; applicationTypeId?: number; companyTypeId?: number }
export interface MenuLayoutWrite { menuId: number; isHidden: boolean; displayOrder: number | null; applicationTypeId?: number; companyTypeId?: number }

export const menuShapeApi = {
  companyTypes: crud<MenuCompanyTypeRead, { menuId: number; companyTypeId: number }>('MenuCompanyType'),
  localizations: crud<MenuLocalizationRead, { menuId: number; languageCode: string; title: string }>('MenuLocalization'),
  applicationLayouts: crud<MenuLayoutRead, MenuLayoutWrite>('MenuApplicationTypeLayout'),
  companyTypeLayouts: crud<MenuLayoutRead, MenuLayoutWrite>('MenuCompanyTypeLayout'),
};

export type DraftChange =
  | { kind: 'hide' | 'show' | 'reset'; menuId: number }
  | { kind: 'order'; menuId: number; order: number }
  | { kind: 'limit'; menuId: number; companyTypeId: number; on: boolean }
  | { kind: 'title'; menuId: number; languageCode: string; title: string };

export interface StudioDraft { applicationTypeId: number; companyTypeId: number | null; note?: string | null; changes: DraftChange[] }
export interface StudioImpact { companies: number; users: number; changes: number; hidden: number; shown: number; reordered: number; limited: number; titles: number }
export interface StudioVerdict { menuId: number; visible: boolean; reason: 'hidden' | 'companyType' | 'feature' | 'module' | 'permission' | null }

export interface StudioNewItem {
  titleEn: string; titleTr?: string | null; route: string; icon?: string | null; parentId?: number | null; applicationTypeId?: number | null;
  permissionCode?: string | null; grantToRoleIds: number[]; companyTypeIds: number[];
}
export interface StudioCreatedItem { menuId: number; code: string; permissionCreated: boolean }

export const menuStudioApi = {
  createItem: (item: StudioNewItem) => http.post<StudioCreatedItem>('/api/MenuStudio/items', item),
  preview: (query: { applicationTypeId: number; companyTypeId?: number | null; roleId?: number | null; userType?: number | null }) =>
    http.get<StudioVerdict[]>('/api/MenuStudio/preview', query),
  impact: (draft: StudioDraft) => http.post<StudioImpact>('/api/MenuStudio/impact', draft),
  publish: (draft: StudioDraft) => http.post<StudioImpact>('/api/MenuStudio/publish', draft),
};
