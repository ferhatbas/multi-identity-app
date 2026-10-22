import type { MenuRead } from '@/features/menus/menus.service';

export type WarningCode = 'broken_route' | 'module_not_granted' | 'empty_group' | 'no_role_holds_permission' | 'platform_only' | 'duplicate_route' | 'hidden';
export type FixKind = 'hide' | 'link_modules' | 'link_roles';

export interface StudioWarning {
  severity: 'warn' | 'info';
  code: WarningCode;
  menuId: number;
  menuCode: string;
  menuTitle: string;
  detail: string | null;
  fixes: FixKind[];
}

export interface WarningInput {
  entries: { menu: MenuRead; hidden: boolean }[];
  grantedModules: ReadonlySet<string>;
  applicationName: string;
  heldPermissionIds: ReadonlySet<number>;
  heldAnywherePermissionIds: ReadonlySet<number>;
  managementPermissionIds: ReadonlySet<number>;
  knownRoutes: ReadonlySet<string>;
}

export function computeWarnings(input: WarningInput): StudioWarning[] {
  const out: StudioWarning[] = [];
  const byParent = new Map<number | null, { menu: MenuRead; hidden: boolean }[]>();
  for (const e of input.entries) { const k = e.menu.parentId; byParent.set(k, [...(byParent.get(k) ?? []), e]); }

  const hasVisibleLeaf = (id: number): boolean => (byParent.get(id) ?? []).some(c => !c.hidden && (c.menu.path ? true : hasVisibleLeaf(c.menu.id)));
  const routes = new Map<string, number>();
  for (const { menu } of input.entries) if (menu.path) routes.set(menu.path, (routes.get(menu.path) ?? 0) + 1);

  const W = (severity: StudioWarning['severity'], code: WarningCode, m: MenuRead, detail: string | null, fixes: FixKind[]): StudioWarning =>
    ({ severity, code, menuId: m.id, menuCode: m.code, menuTitle: m.title, detail, fixes });

  for (const { menu, hidden } of input.entries) {
    if (hidden) { out.push(W('info', 'hidden', menu, null, [])); continue; }
    if (menu.path && !input.knownRoutes.has(menu.path)) out.push(W('warn', 'broken_route', menu, menu.path, ['hide']));
    if (menu.moduleCode && !input.grantedModules.has(menu.moduleCode)) out.push(W('warn', 'module_not_granted', menu, `${menu.moduleCode} → ${input.applicationName}`, ['link_modules', 'hide']));
    if (!menu.path && !hasVisibleLeaf(menu.id)) out.push(W('warn', 'empty_group', menu, null, ['hide']));
    if (menu.permissionId !== null && !input.managementPermissionIds.has(menu.permissionId) && !input.heldPermissionIds.has(menu.permissionId)) {
      if (input.heldAnywherePermissionIds.has(menu.permissionId)) out.push(W('warn', 'no_role_holds_permission', menu, null, ['link_roles']));
      else out.push(W('info', 'platform_only', menu, null, []));
    }
    if (menu.path && (routes.get(menu.path) ?? 0) > 1) out.push(W('info', 'duplicate_route', menu, menu.path, []));
  }

  return out.sort((a, b) => (a.severity === b.severity ? 0 : a.severity === 'warn' ? -1 : 1));
}
