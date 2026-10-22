import { useEffect, useMemo, useState } from 'react';
import { SearchBox } from '@/components/ui/DataTable';
import { Spinner } from '@/components/ui/Form';
import type { ApplicationTypeRead, CompanyTypeApplicationRead, CompanyTypeRead } from '@/features/definitions/definitions.service';
import { menuStudioApi, type MenuRead, type StudioVerdict } from '@/features/menus/menus.service';
import type { RoleRead } from '@/features/roles/roles.service';
import { useI18n } from '@/i18n';

type Axis = 'companyType' | 'application' | 'role';
type Cell = 'present' | 'absent' | 'locked' | 'visible' | 'hidden';

export function MenuStudioMatrix({ menus, apps, companyTypes, roles, typeApplications, grantedModules, appId, typeId, draftTypeId, dirty, limitsOf, hiddenOf, onToggleType, onInspect }: {
  menus: MenuRead[]; apps: ApplicationTypeRead[]; companyTypes: CompanyTypeRead[]; roles: RoleRead[]; typeApplications: CompanyTypeApplicationRead[];
  grantedModules: (applicationId: number) => ReadonlySet<string>;
  appId: number; typeId: number | null;
  draftTypeId: number | null;
  dirty?: boolean;
  limitsOf: (menu: MenuRead) => Set<number>;
  hiddenOf: (menu: MenuRead) => boolean;
  onToggleType: (menu: MenuRead, companyTypeId: number, present: boolean) => void;
  onInspect: (roleId: number, menuId: number) => void;
}) {
  const { t } = useI18n();
  const [axis, setAxis] = useState<Axis>('companyType');
  const [filter, setFilter] = useState('');
  const [verdicts, setVerdicts] = useState<Map<number, Map<number, StudioVerdict>> | null>(null);

  const usableRoles = useMemo(() => roles.filter(r => r.applicationTypeId === null || r.applicationTypeId === appId), [roles, appId]);

  useEffect(() => {
    if (axis !== 'role') return;
    setVerdicts(null);
    Promise.all(usableRoles.map(r => menuStudioApi.preview({ applicationTypeId: appId, companyTypeId: typeId, roleId: r.id }).then(list => [r.id, new Map(list.map(v => [v.menuId, v]))] as const)))
      .then(pairs => setVerdicts(new Map(pairs))).catch(() => setVerdicts(new Map()));
  }, [axis, appId, typeId, usableRoles]);

  const rows = useMemo(() => {
    const byId = new Map(menus.map(m => [m.id, m]));
    const depth = (m: MenuRead): number => (m.parentId && byId.get(m.parentId) ? 1 + depth(byId.get(m.parentId)!) : 0);
    const ordered: MenuRead[] = [];
    const walk = (parent: number | null) => menus.filter(m => (m.parentId ?? null) === parent).sort((a, b) => a.displayOrder - b.displayOrder).forEach(m => { ordered.push(m); walk(m.id); });
    walk(null);
    const q = filter.trim().toLowerCase();
    return ordered.filter(m => !q || m.title.toLowerCase().includes(q) || m.code.toLowerCase().includes(q) || (m.path ?? '').toLowerCase().includes(q)).map(m => ({ menu: m, depth: depth(m) }));
  }, [menus, filter]);

  const typeCell = (m: MenuRead, ct: CompanyTypeRead): Cell => {
    if (m.applicationTypeId !== null && !typeApplications.some(x => x.companyTypeId === ct.id && x.applicationTypeId === m.applicationTypeId)) return 'locked';
    const limited = limitsOf(m);
    return limited.size === 0 || limited.has(ct.id) ? 'present' : 'absent';
  };
  const appCell = (m: MenuRead, a: ApplicationTypeRead): Cell => {
    if (m.applicationTypeId !== null && m.applicationTypeId !== a.id) return 'absent';
    if (m.moduleCode && !grantedModules(a.id).has(m.moduleCode)) return 'locked';
    return 'present';
  };
  const roleCell = (m: MenuRead, r: RoleRead): Cell => {
    const v = verdicts?.get(r.id)?.get(m.id);
    if (!v) return 'absent';
    return v.visible ? 'visible' : v.reason === 'permission' ? 'hidden' : 'locked';
  };

  const columns: { id: number; label: string; title: string }[] = axis === 'companyType' ? companyTypes.map(c => ({ id: c.id, label: c.code, title: c.name }))
    : axis === 'application' ? apps.map(a => ({ id: a.id, label: a.code, title: a.name })) : usableRoles.map(r => ({ id: r.id, label: r.name, title: r.name }));

  const glyph = (cell: Cell) => (cell === 'present' || cell === 'visible' ? <i className="pi pi-check" style={{ color: 'var(--success, #16a34a)', fontSize: 12 }} />
    : cell === 'hidden' ? <i className="pi pi-eye-slash" style={{ color: '#d97706', fontSize: 12 }} />
    : cell === 'locked' ? <i className="pi pi-lock muted-text" style={{ fontSize: 11 }} /> : <span className="matrix-dot" />);

  const legend: [Cell, string][] = axis === 'role' ? [['visible', t('sees it')], ['hidden', t('hidden from this role')], ['locked', t('hidden from everyone')], ['absent', t('not in the menu')]]
    : axis === 'application' ? [['present', t('in the menu')], ['absent', t('not in its tree')], ['locked', t('module not granted')]]
    : [['present', t('in the menu')], ['absent', t('not in the menu')], ['locked', t('no application of the type reaches it')]];

  return (
    <div className="card">
      <div className="toolbar" style={{ padding: '12px 14px 0' }}>
        <strong><i className="pi pi-table" /> {t('Matrix')}</strong>
        <span className="segmented">
          {(['companyType', 'application', 'role'] as Axis[]).map(a => (
            <button key={a} type="button" className={axis === a ? 'is-on' : ''} onClick={() => setAxis(a)}>{a === 'companyType' ? t('by company type') : a === 'application' ? t('by application') : t('by role')}</button>
          ))}
        </span>
        <SearchBox onChange={setFilter} placeholder={t('Filter rows…')} />
        <div className="grow" />
        {legend.map(([cell, label]) => <span key={cell} className="row muted-text" style={{ gap: 4 }}>{glyph(cell)} {label}</span>)}
      </div>
      {axis === 'role' && <p className="muted-text" style={{ padding: '0 14px' }}>{t('Roles usable in this application; click a cell to open the tree through that role’s eyes.')}</p>}
      {axis === 'companyType' && (
        <p className="muted-text" style={{ padding: '0 14px' }}>
          {draftTypeId !== null
            ? t('The {type} column goes into the draft; every other column is written at once.', { type: companyTypes.find(c => c.id === draftTypeId)?.name ?? '' })
            : t('With no company type chosen, every column is written at once.')}
          {dirty ? ` ${t('Unpublished changes are shown in the draft column.')}` : ''}
        </p>
      )}
      {axis === 'role' && !verdicts ? <Spinner label={t('Loading…')} /> : (
        <div style={{ overflow: 'auto', maxHeight: 640 }}>
          <table className="access-matrix">
            <thead>
              <tr>
                <th className="sticky-col">{t('Item')}</th>
                {columns.map(c => <th key={c.id} title={c.title} className={axis === 'companyType' && c.id === draftTypeId ? 'is-draft-col' : undefined}>{c.label}</th>)}
              </tr>
            </thead>
            <tbody>
              {rows.map(({ menu, depth }) => (
                <tr key={menu.id} className={hiddenOf(menu) ? 'is-dim' : undefined}>
                  <th className="sticky-col" style={{ paddingLeft: 12 + depth * 14 }}>
                    <b style={{ fontWeight: menu.path ? 500 : 700 }}>{menu.title}</b> {menu.moduleCode && <span className="badge-mini">{menu.moduleCode}</span>}
                  </th>
                  {axis === 'companyType' && companyTypes.map(ct => { const cell = typeCell(menu, ct); return <td key={ct.id}><button type="button" className="cell-btn" disabled={cell === 'locked'} onClick={() => onToggleType(menu, ct.id, cell === 'present')}>{glyph(cell)}</button></td>; })}
                  {axis === 'application' && apps.map(a => <td key={a.id}>{glyph(appCell(menu, a))}</td>)}
                  {axis === 'role' && usableRoles.map(r => { const cell = roleCell(menu, r); return <td key={r.id}><button type="button" className="cell-btn" disabled={cell === 'absent'} onClick={() => onInspect(r.id, menu.id)}>{glyph(cell)}</button></td>; })}
                </tr>
              ))}
            </tbody>
          </table>
          {rows.length === 0 && <p className="muted-text" style={{ padding: 16 }}>{t('No row matches.')}</p>}
        </div>
      )}
    </div>
  );
}
