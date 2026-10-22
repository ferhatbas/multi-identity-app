import { useCallback, useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { Button } from 'primereact/button';
import { Checkbox } from 'primereact/checkbox';
import { InputTextarea } from 'primereact/inputtextarea';
import { Badge, EmptyState, PageHeader, SelectField, Spinner } from '@/components/ui/Form';
import { Modal } from '@/components/ui/Modal';
import { useNavigationGuard } from '@/components/ui/useNavigationGuard';
import { SearchBox } from '@/components/ui/DataTable';
import { useToast } from '@/components/ui/Toast';
import { useAuth } from '@/features/auth/AuthContext';
import { P } from '@/features/auth/permissions';
import {
  definitionsApi, type AppModuleRead, type ApplicationModuleRead, type ApplicationTypeRead, type CompanyTypeApplicationRead, type CompanyTypeRead,
} from '@/features/definitions/definitions.service';
import {
  menuShapeApi, menuStudioApi, menusApi, type DraftChange, type MenuCompanyTypeRead, type MenuLayoutRead, type MenuLocalizationRead, type MenuRead,
  type StudioImpact, type StudioVerdict,
} from '@/features/menus/menus.service';
import { rolesApi, type PermissionRead, type RolePermissionRead, type RoleRead } from '@/features/roles/roles.service';
import { KNOWN_ROUTES } from '@/routes/manifest';
import { UserTypeNames } from '@/types/api';
import { useI18n } from '@/i18n';
import { MenuStudioMatrix } from './MenuStudioMatrix';
import { MenuStudioNewItemDialog } from './MenuStudioNewItemDialog';
import { computeWarnings, type FixKind } from './menuStudioWarnings';

interface Node { menu: MenuRead; order: number; hidden: boolean; children: Node[] }

function addChange(list: DraftChange[], change: DraftChange): DraftChange[] {
  const same = (c: DraftChange) => c.menuId === change.menuId;
  const visibility = (c: DraftChange) => c.kind === 'hide' || c.kind === 'show' || c.kind === 'reset';
  if (change.kind === 'reset') return [...list.filter(c => !(same(c) && (visibility(c) || c.kind === 'order'))), change];
  if (change.kind === 'hide' || change.kind === 'show') return [...list.filter(c => !(same(c) && (c.kind === 'hide' || c.kind === 'show'))), change];
  if (change.kind === 'order') return [...list.filter(c => !(same(c) && c.kind === 'order')), change];
  if (change.kind === 'limit') return [...list.filter(c => !(c.kind === 'limit' && same(c) && c.companyTypeId === change.companyTypeId)), change];
  if (change.kind === 'title') return [...list.filter(c => !(c.kind === 'title' && same(c) && c.languageCode === change.languageCode)), change];
  return [...list, change];
}

export function MenuStudioPage() {
  const { t } = useI18n();
  const toast = useToast();
  const { can } = useAuth();

  const [menus, setMenus] = useState<MenuRead[]>([]);
  const [apps, setApps] = useState<ApplicationTypeRead[]>([]);
  const [companyTypes, setCompanyTypes] = useState<CompanyTypeRead[]>([]);
  const [limits, setLimits] = useState<MenuCompanyTypeRead[]>([]);
  const [appLayouts, setAppLayouts] = useState<MenuLayoutRead[]>([]);
  const [typeLayouts, setTypeLayouts] = useState<MenuLayoutRead[]>([]);
  const [titles, setTitles] = useState<MenuLocalizationRead[]>([]);
  const [languages, setLanguages] = useState<{ code: string; name: string }[]>([]);
  const [perms, setPerms] = useState<PermissionRead[]>([]);
  const [roles, setRoles] = useState<RoleRead[]>([]);
  const [appModules, setAppModules] = useState<AppModuleRead[]>([]);
  const [applicationModules, setApplicationModules] = useState<ApplicationModuleRead[]>([]);
  const [typeApplications, setTypeApplications] = useState<CompanyTypeApplicationRead[]>([]);
  const [grants, setGrants] = useState<RolePermissionRead[]>([]);
  const [loading, setLoading] = useState(true);
  const [view, setView] = useState<'tree' | 'matrix'>('tree');
  const [newItem, setNewItem] = useState(false);
  const [copying, setCopying] = useState(false);
  const [warningsOpen, setWarningsOpen] = useState(false);
  const [tick, setTick] = useState(0);

  const [appId, setAppId] = useState<number | null>(null);
  const [typeId, setTypeId] = useState<number | null>(null);
  const [roleId, setRoleId] = useState<number | null>(null);
  const [rank, setRank] = useState<number | null>(null);
  const [selected, setSelected] = useState<number | null>(null);
  const [search, setSearch] = useState('');
  const [dragging, setDragging] = useState<number | null>(null);

  const [changes, setChanges] = useState<DraftChange[]>([]);
  const [verdicts, setVerdicts] = useState<Map<number, StudioVerdict>>(new Map());
  const [publishing, setPublishing] = useState<{ impact: StudioImpact | null; note: string; busy: boolean } | null>(null);

  const dirty = changes.length > 0;

  const load = useCallback(async () => {
    try {
      const [m, a, ct, lm, al, tl, lt, lg, pe, ro, am, apm, cta, gr] = await Promise.all([
        menusApi.list(), definitionsApi.applicationTypes.list(), definitionsApi.companyTypes.list(), menuShapeApi.companyTypes.list(), menuShapeApi.applicationLayouts.list(),
        menuShapeApi.companyTypeLayouts.list(), menuShapeApi.localizations.list(), definitionsApi.languages.list(), rolesApi.permissions.list(), rolesApi.list(),
        definitionsApi.appModules.list().catch(() => []), definitionsApi.applicationModules.list().catch(() => []), definitionsApi.companyTypeApplications.list().catch(() => []), rolesApi.grants.list().catch(() => []),
      ]);
      setMenus(m); setApps(a); setCompanyTypes(ct); setLimits(lm); setAppLayouts(al); setTypeLayouts(tl); setTitles(lt); setLanguages(lg); setPerms(pe); setRoles(ro);
      setAppModules(am); setApplicationModules(apm); setTypeApplications(cta); setGrants(gr);
      setAppId(current => current ?? a[0]?.id ?? null);
    } catch (e) { toast.error((e as Error).message); } finally { setLoading(false); }
  }, []); // eslint-disable-line react-hooks/exhaustive-deps
  useEffect(() => { void load(); }, [load]);

  useNavigationGuard(dirty, t('There are unpublished changes. Leave anyway?'));

  useEffect(() => {
    if (appId === null) return;
    menuStudioApi.preview({ applicationTypeId: appId, companyTypeId: typeId, roleId, userType: rank })
      .then(list => setVerdicts(new Map(list.map(v => [v.menuId, v])))).catch(() => setVerdicts(new Map()));
  }, [appId, typeId, roleId, rank, changes.length === 0, tick]); // eslint-disable-line react-hooks/exhaustive-deps

  const confirmDiscard = () => !dirty || window.confirm(t('There are unpublished changes. Discard them?'));

  const changeContext = (next: () => void) => { if (!confirmDiscard()) return; setChanges([]); setSelected(null); next(); };

  const baseLayout = useCallback((m: MenuRead): MenuLayoutRead | undefined =>
    (typeId !== null ? typeLayouts.find(l => l.menuId === m.id && l.companyTypeId === typeId) : undefined) ?? appLayouts.find(l => l.menuId === m.id && l.applicationTypeId === appId),
  [typeId, appId, typeLayouts, appLayouts]);

  const limitsOf = useCallback((m: MenuRead): Set<number> => {
    const set = new Set(limits.filter(l => l.menuId === m.id).map(l => l.companyTypeId));
    for (const c of changes) if (c.kind === 'limit' && c.menuId === m.id) { if (c.on) set.add(c.companyTypeId); else set.delete(c.companyTypeId); }
    return set;
  }, [limits, changes]);

  const effective = useCallback((m: MenuRead) => {
    const base = baseLayout(m);
    let hidden = !!base?.isHidden;
    let order = base?.displayOrder ?? m.displayOrder;
    for (const c of changes) {
      if (c.menuId !== m.id) continue;
      if (c.kind === 'hide') hidden = true;
      else if (c.kind === 'show') hidden = false;
      else if (c.kind === 'order') order = c.order;
      else if (c.kind === 'reset') {
        const fallback = typeId !== null ? appLayouts.find(l => l.menuId === m.id && l.applicationTypeId === appId) : undefined;
        hidden = !!fallback?.isHidden; order = fallback?.displayOrder ?? m.displayOrder;
      }
    }
    return { hidden: hidden || !m.isActive, order };
  }, [baseLayout, changes, typeId, appId, appLayouts]);

  const inContext = useCallback((m: MenuRead) => {
    if (m.applicationTypeId !== null && m.applicationTypeId !== appId) return false;
    if (typeId === null) return true;
    const set = limitsOf(m);
    return set.size === 0 || set.has(typeId);
  }, [appId, typeId, limitsOf]);

  const titleOf = useCallback((m: MenuRead, code: string) => {
    const drafted = [...changes].reverse().find(c => c.kind === 'title' && c.menuId === m.id && c.languageCode === code);
    return drafted && drafted.kind === 'title' ? drafted.title : titles.find(x => x.menuId === m.id && x.languageCode === code)?.title ?? '';
  }, [changes, titles]);

  const tree = useMemo<Node[]>(() => {
    const nodes = new Map<number, Node>();
    for (const m of menus.filter(inContext)) { const e = effective(m); nodes.set(m.id, { menu: m, order: e.order, hidden: e.hidden, children: [] }); }
    const roots: Node[] = [];
    for (const n of nodes.values()) (n.menu.parentId ? nodes.get(n.menu.parentId) : undefined)?.children.push(n) ?? roots.push(n);
    const sort = (list: Node[]) => { list.sort((a, b) => a.order - b.order || a.menu.title.localeCompare(b.menu.title)); list.forEach(n => sort(n.children)); };
    sort(roots);
    return roots;
  }, [menus, inContext, effective]);

  const placed = useMemo(() => new Set(menus.filter(inContext).map(m => m.id)), [menus, inContext]);
  const catalogue = useMemo(() => {
    const q = search.trim().toLowerCase();
    return menus.filter(m => !placed.has(m.id) && (!q || m.title.toLowerCase().includes(q) || m.code.toLowerCase().includes(q)));
  }, [menus, placed, search]);

  const current = menus.find(m => m.id === selected) ?? null;
  const push = (c: DraftChange) => setChanges(list => addChange(list, c));

  const reorder = (siblings: Node[], from: number, to: number) => {
    if (from === to || from < 0 || to < 0) return;
    const list = [...siblings];
    const [moved] = list.splice(from, 1);
    list.splice(to, 0, moved);
    setChanges(current => list.reduce((acc, n, i) => ((i + 1) * 10 === n.order ? acc : addChange(acc, { kind: 'order', menuId: n.menu.id, order: (i + 1) * 10 })), current));
  };

  const hasOwnRow = (m: MenuRead) => !!(typeId !== null ? typeLayouts.find(l => l.menuId === m.id && l.companyTypeId === typeId) : appLayouts.find(l => l.menuId === m.id && l.applicationTypeId === appId))
    || changes.some(c => c.menuId === m.id && (c.kind === 'hide' || c.kind === 'show' || c.kind === 'order'));

  const grantedModulesOf = useCallback((applicationId: number): ReadonlySet<string> => new Set([
    ...appModules.filter(m => m.isCore).map(m => m.code),
    ...applicationModules.filter(x => x.applicationTypeId === applicationId).map(x => appModules.find(m => m.id === x.appModuleId)?.code).filter((c): c is string => !!c),
  ]), [appModules, applicationModules]);

  const warnings = useMemo(() => (appId === null ? [] : computeWarnings({
    entries: menus.filter(inContext).map(m => ({ menu: m, hidden: effective(m).hidden })),
    grantedModules: grantedModulesOf(appId),
    applicationName: apps.find(a => a.id === appId)?.name ?? '',
    heldPermissionIds: new Set(grants.filter(g => roles.some(r => r.id === g.roleId && (r.applicationTypeId === null || r.applicationTypeId === appId))).map(g => g.permissionId)),
    heldAnywherePermissionIds: new Set(grants.map(g => g.permissionId)),
    managementPermissionIds: new Set(perms.filter(p => p.isManagement).map(p => p.id)),
    knownRoutes: KNOWN_ROUTES,
  })), [appId, menus, inContext, effective, grantedModulesOf, apps, grants, roles, perms]);
  const warnCount = warnings.filter(w => w.severity === 'warn').length;

  const warningText = (code: string) => (code === 'broken_route' ? t('Opens a screen the app does not have') : code === 'module_not_granted' ? t('Its module is not granted to this application')
    : code === 'empty_group' ? t('A group with nothing visible under it') : code === 'no_role_holds_permission' ? t('No role of this application holds its permission')
    : code === 'duplicate_route' ? t('Another entry opens the same screen') : code === 'platform_only' ? t('Only platform administrators see it') : t('Switched off here'));

  const applyFix = (fix: FixKind, menuId: number) => {
    if (fix === 'hide') push({ kind: 'hide', menuId });
    else if (!confirmDiscard()) return;
    else { window.location.assign(fix === 'link_modules' ? '/admin/application-types' : '/admin/access'); }
  };

  const limitChanges = (menu: MenuRead, companyTypeId: number, present: boolean, from: Set<number>): DraftChange[] | null => {
    const all = companyTypes.map(c => c.id);
    const now = new Set(from.size === 0 ? all : from);
    if (present) now.delete(companyTypeId); else now.add(companyTypeId);
    if (now.size === 0) { toast.error(t('An entry must keep at least one company type')); return null; }
    const wanted = now.size === all.length ? new Set<number>() : now;
    return all.filter(id => from.has(id) !== wanted.has(id)).map(id => ({ kind: 'limit' as const, menuId: menu.id, companyTypeId: id, on: wanted.has(id) }));
  };

  const toggleType = async (menu: MenuRead, companyTypeId: number, present: boolean) => {
    if (typeId !== null && companyTypeId === typeId) {
      const changes = limitChanges(menu, companyTypeId, present, limitsOf(menu));
      changes?.forEach(push);
      return;
    }
    const stored = new Set(limits.filter(l => l.menuId === menu.id).map(l => l.companyTypeId));
    const changes = limitChanges(menu, companyTypeId, present, stored);
    if (!changes || changes.length === 0 || appId === null) return;
    try {
      await menuStudioApi.publish({ applicationTypeId: appId, companyTypeId: null, note: t('Changed in the matrix'), changes });
      toast.success(present
        ? t('{item} removed from {type}', { item: menu.title, type: companyTypes.find(c => c.id === companyTypeId)?.name ?? '' })
        : t('{item} added to {type}', { item: menu.title, type: companyTypes.find(c => c.id === companyTypeId)?.name ?? '' }));
      await load();
    } catch (e) { toast.error((e as Error).message); }
  };

  const copySources = typeId !== null ? companyTypes.filter(c => c.id !== typeId).map(c => ({ id: c.id, name: c.name, count: typeLayouts.filter(l => l.companyTypeId === c.id).length }))
    : apps.filter(a => a.id !== appId).map(a => ({ id: a.id, name: a.name, count: appLayouts.filter(l => l.applicationTypeId === a.id).length }));

  const copyLayout = (sourceId: number) => {
    const rows = typeId !== null ? typeLayouts.filter(l => l.companyTypeId === sourceId) : appLayouts.filter(l => l.applicationTypeId === sourceId);
    setChanges(list => rows.filter(r => placed.has(r.menuId)).reduce((acc, r) => {
      let next = addChange(acc, { kind: r.isHidden ? 'hide' : 'show', menuId: r.menuId });
      if (r.displayOrder !== null) next = addChange(next, { kind: 'order', menuId: r.menuId, order: r.displayOrder });
      return next;
    }, list));
    setCopying(false);
    toast.success(t('Layout copied into the draft'));
  };

  const canGrant = can(P.RoleManage);
  const givePermission = async (menu: MenuRead) => {
    if (roleId === null || menu.permissionId === null) return;
    try { await rolesApi.grants.create({ roleId, permissionId: menu.permissionId }); setGrants(await rolesApi.grants.list()); setTick(n => n + 1); toast.success(t('Permission given')); }
    catch (e) { toast.error((e as Error).message); }
  };

  const draft = () => ({ applicationTypeId: appId!, companyTypeId: typeId, note: publishing?.note.trim() || null, changes });

  const openPublish = async () => {
    setPublishing({ impact: null, note: '', busy: false });
    try { const impact = await menuStudioApi.impact({ applicationTypeId: appId!, companyTypeId: typeId, changes }); setPublishing(p => (p ? { ...p, impact } : p)); }
    catch (e) { toast.error((e as Error).message); setPublishing(null); }
  };

  const publish = async () => {
    setPublishing(p => (p ? { ...p, busy: true } : p));
    try { await menuStudioApi.publish(draft()); toast.success(t('Menu published')); setPublishing(null); setChanges([]); await load(); }
    catch (e) { toast.error((e as Error).message); setPublishing(p => (p ? { ...p, busy: false } : p)); }
  };

  const verdictOf = (id: number) => verdicts.get(id);
  const reasonText = (reason: StudioVerdict['reason']) => (reason === 'hidden' ? t('Hidden here') : reason === 'companyType' ? t('Not for this company type') : reason === 'feature' ? t('Feature switched off')
    : reason === 'module' ? t('Module not granted') : t('Needs a permission'));
  const imagining = roleId !== null || rank !== null;
  const scopeLabel = typeId !== null ? t('this company type') : t('this application');

  const row = (n: Node, siblings: Node[], index: number, depth: number) => {
    const verdict = verdictOf(n.menu.id);
    const blocked = imagining && verdict && !verdict.visible && verdict.reason !== 'hidden';
    return (
      <div key={n.menu.id}>
        <div className={`menu-row${selected === n.menu.id ? ' is-selected' : ''}${n.hidden || blocked ? ' is-dim' : ''}${dragging === n.menu.id ? ' is-dragging' : ''}`}
             style={{ paddingLeft: 8 + depth * 20 }} draggable onClick={() => setSelected(n.menu.id)}
             onDragStart={() => setDragging(n.menu.id)} onDragEnd={() => setDragging(null)}
             onDragOver={e => { if (dragging !== null && siblings.some(s => s.menu.id === dragging)) e.preventDefault(); }}
             onDrop={e => { e.preventDefault(); if (dragging !== null) reorder(siblings, siblings.findIndex(s => s.menu.id === dragging), index); setDragging(null); }}>
          <i className="pi pi-bars drag-handle" aria-hidden="true" />
          <i className={`pi pi-${n.menu.icon ?? (n.children.length ? 'folder' : 'minus')}`} />
          <span className="grow"><b>{n.menu.title}</b> <span className="muted-text">· {n.menu.code}</span></span>
          {blocked && <Badge tone="warning">{reasonText(verdict!.reason)}</Badge>}
          {n.hidden && <Badge>{t('Hidden')}</Badge>}
          {limitsOf(n.menu).size > 0 && <Badge tone="info">{t('Limited')}</Badge>}
          <Button text rounded size="small" severity="secondary" icon="pi pi-arrow-up" aria-label={t('Move up')} disabled={index === 0} onClick={e => { e.stopPropagation(); reorder(siblings, index, index - 1); }} />
          <Button text rounded size="small" severity="secondary" icon="pi pi-arrow-down" aria-label={t('Move down')} disabled={index === siblings.length - 1} onClick={e => { e.stopPropagation(); reorder(siblings, index, index + 1); }} />
          <Button text rounded size="small" severity="secondary" icon={n.hidden ? 'pi pi-eye' : 'pi pi-eye-slash'} aria-label={n.hidden ? t('Show') : t('Hide')} disabled={!n.menu.isActive}
                  onClick={e => { e.stopPropagation(); push({ kind: n.hidden ? 'show' : 'hide', menuId: n.menu.id }); }} />
        </div>
        {n.children.map((c, i) => row(c, n.children, i, depth + 1))}
      </div>
    );
  };

  if (loading) return <Spinner label={t('Loading…')} />;

  return (
    <>
      <PageHeader title={t('Menu Studio')} subtitle={t('Which menu an application and a company type get, and in what order.')}
                  actions={<>
                    {can(P.MenuDesign) && <Button size="small" icon="pi pi-plus" label={t('New item')} onClick={() => setNewItem(true)} />}
                    <Button size="small" outlined severity="secondary" icon="pi pi-copy" label={t('Copy layout from…')} disabled={copySources.length === 0 || appId === null} onClick={() => setCopying(true)} />
                    <Link to="/admin/menus" className="p-button p-button-outlined p-button-secondary p-button-sm" onClick={e => { if (!confirmDiscard()) e.preventDefault(); }}><i className="pi pi-list" style={{ marginRight: 6 }} />{t('Menu management')}</Link>
                  </>} />

      <div className="card card-pad row row-wrap" style={{ marginBottom: 14, alignItems: 'flex-end' }}>
        <div style={{ width: 220 }}>
          <SelectField label={t('Application')} value={appId} options={apps.map(a => ({ value: a.id, label: a.name }))} onChange={e => changeContext(() => setAppId(e.target.value ? Number(e.target.value) : null))} />
        </div>
        <div style={{ width: 220 }}>
          <SelectField label={t('Company type')} placeholder={t('All company types')} value={typeId} options={companyTypes.map(c => ({ value: c.id, label: c.name }))} onChange={e => changeContext(() => setTypeId(e.target.value ? Number(e.target.value) : null))} />
        </div>
        <div style={{ width: 200 }}>
          <SelectField label={t('See it as role')} placeholder={t('Nobody')} value={roleId} options={roles.filter(r => r.applicationTypeId === null || r.applicationTypeId === appId).map(r => ({ value: r.id, label: r.name }))} onChange={e => setRoleId(e.target.value ? Number(e.target.value) : null)} />
        </div>
        <div style={{ width: 180 }}>
          <SelectField label={t('and rank')} placeholder="—" value={rank} options={UserTypeNames.slice(0, 4).map((n, i) => ({ value: i, label: n }))} onChange={e => setRank(e.target.value === '' ? null : Number(e.target.value))} />
        </div>
      </div>

      <div className="row row-wrap" style={{ marginBottom: 12 }}>
        <span className="segmented">
          <button type="button" className={view === 'tree' ? 'is-on' : ''} onClick={() => setView('tree')}><i className="pi pi-sitemap" /> {t('Tree')}</button>
          <button type="button" className={view === 'matrix' ? 'is-on' : ''} onClick={() => setView('matrix')}><i className="pi pi-table" /> {t('Matrix')}</button>
        </span>
        {warnings.length > 0 && (
          <button type="button" className={`warn-chip${warnCount > 0 ? ' has-warnings' : ''}`} onClick={() => setWarningsOpen(o => !o)}>
            <i className="pi pi-exclamation-triangle" /> {t('{warn} warnings, {info} notes', { warn: warnCount, info: warnings.length - warnCount })} <i className={`pi pi-angle-${warningsOpen ? 'up' : 'down'}`} />
          </button>
        )}
      </div>
      {warningsOpen && warnings.length > 0 && (
        <div className="card card-pad stack" style={{ marginBottom: 14 }}>
          {warnings.map((w, i) => (
            <div key={`${w.menuId}-${w.code}-${i}`} className="row row-wrap" style={{ gap: 8 }}>
              <Badge tone={w.severity === 'warn' ? 'warning' : 'neutral'}>{w.severity === 'warn' ? t('Warning') : t('Note')}</Badge>
              <a href="#" onClick={e => { e.preventDefault(); setSelected(w.menuId); setView('tree'); }}><b>{w.menuTitle}</b></a>
              <span className="muted-text">{warningText(w.code)}{w.detail ? ` — ${w.detail}` : ''}</span>
              <div className="grow" />
              {w.fixes.map(f => <Button key={f} text size="small" label={f === 'hide' ? t('Hide it') : f === 'link_modules' ? t('Open application types') : t('Open the Authorisation Studio')} onClick={() => applyFix(f, w.menuId)} />)}
            </div>
          ))}
        </div>
      )}

      {dirty && (
        <div className="card card-pad row row-wrap draft-bar" role="status">
          <i className="pi pi-pencil" />
          <strong>{t('{n} unpublished changes', { n: changes.length })}</strong>
          <span className="muted-text">{t('They apply to {scope} once published.', { scope: scopeLabel })}</span>
          <div className="grow" />
          <Button text size="small" severity="secondary" icon="pi pi-undo" label={t('Undo')} onClick={() => setChanges(list => list.slice(0, -1))} />
          <Button text size="small" severity="secondary" label={t('Discard')} onClick={() => { if (confirmDiscard()) setChanges([]); }} />
          <Button size="small" icon="pi pi-send" label={t('Publish')} onClick={() => void openPublish()} />
        </div>
      )}

      {view === 'matrix' && appId !== null ? (
        <MenuStudioMatrix menus={menus.filter(m => m.applicationTypeId === null || m.applicationTypeId === appId)} apps={apps} companyTypes={companyTypes} roles={roles} typeApplications={typeApplications}
                          grantedModules={grantedModulesOf} appId={appId} typeId={typeId} limitsOf={limitsOf} hiddenOf={m => effective(m).hidden}
                          onToggleType={(m, ct, present) => void toggleType(m, ct, present)} draftTypeId={typeId} dirty={dirty} onInspect={(role, menuId) => { setRoleId(role); setSelected(menuId); setView('tree'); }} />
      ) : (
      <div className="studio">
        <div className="card studio-col">
          <div className="card-title" style={{ padding: '12px 14px 0' }}><h3>{t('Not in this menu')}</h3></div>
          <div style={{ padding: '0 14px' }}><SearchBox onChange={setSearch} placeholder={t('Search entries…')} /></div>
          {catalogue.length === 0 ? <EmptyState title={t('Everything is in this menu')} /> : catalogue.map(m => (
            <div key={m.id} className={`menu-row${selected === m.id ? ' is-selected' : ''}`} onClick={() => setSelected(m.id)}>
              <span className="grow"><b>{m.title}</b> <span className="muted-text">· {m.code}</span></span>
              <span className="muted-text">{m.applicationTypeId ? apps.find(a => a.id === m.applicationTypeId)?.name : t('Shared')}</span>
            </div>
          ))}
        </div>

        <div className="card studio-col">
          <div className="card-title" style={{ padding: '12px 14px 0' }}><h3>{t('The menu')}</h3></div>
          {tree.length === 0 ? <EmptyState title={t('This menu is empty')} /> : tree.map((n, i) => row(n, tree, i, 0))}
        </div>

        <div className="card studio-col card-pad">
          {!current ? <EmptyState title={t('Select an entry')} text={t('Its limits and titles appear here.')} /> : (
            <div className="stack">
              <div><h3>{current.title}</h3><div className="muted-text"><code>{current.code}</code>{current.path ? <> · <code>{current.path}</code></> : null}</div></div>
              {imagining && verdictOf(current.id) && (
                <div className={`alert ${verdictOf(current.id)!.visible ? 'alert-success' : 'alert-warning'}`}>
                  {verdictOf(current.id)!.visible ? t('They see this entry.') : reasonText(verdictOf(current.id)!.reason)}
                  {!verdictOf(current.id)!.visible && verdictOf(current.id)!.reason === 'permission' && roleId !== null && current.permissionId !== null && canGrant && (
                    <Button text size="small" icon="pi pi-key" label={t('Give {permission} to {role}', { permission: perms.find(p => p.id === current.permissionId)?.code ?? '', role: roles.find(r => r.id === roleId)?.name ?? '' })}
                            onClick={() => void givePermission(current)} />
                  )}
                </div>
              )}
              <dl className="kv">
                <dt>{t('Application')}</dt><dd>{current.applicationTypeId ? apps.find(a => a.id === current.applicationTypeId)?.name : t('Shared')}</dd>
                <dt>{t('Module')}</dt><dd>{current.moduleCode ?? '—'}</dd>
                <dt>{t('Required permission')}</dt><dd>{perms.find(p => p.id === current.permissionId)?.code ?? t('None')}</dd>
                <dt>{t('Feature switch')}</dt><dd>{current.featureCode ?? '—'}</dd>
              </dl>

              <div>
                <strong>{t('Company types')}</strong>
                <p className="muted-text">{t('With none ticked the entry is for every company type.')}</p>
                {companyTypes.map(ct => (
                  <label key={ct.id} className="check"><Checkbox checked={limitsOf(current).has(ct.id)} onChange={e => push({ kind: 'limit', menuId: current.id, companyTypeId: ct.id, on: !!e.checked })} /><span>{ct.name}</span></label>
                ))}
              </div>

              <div>
                <strong>{t('Titles')}</strong>
                <p className="muted-text">{t('Empty uses the entry’s own name: {name}.', { name: current.title })}</p>
                {languages.map(l => <TitleRow key={l.code} language={l} value={titleOf(current, l.code)} onChange={v => push({ kind: 'title', menuId: current.id, languageCode: l.code, title: v })} />)}
              </div>

              {placed.has(current.id) && hasOwnRow(current) && (
                <Button size="small" outlined severity="secondary" icon="pi pi-replay" label={t('Reset order and visibility here')} onClick={() => push({ kind: 'reset', menuId: current.id })} />
              )}
            </div>
          )}
        </div>
      </div>
      )}

      {newItem && appId !== null && (
        <MenuStudioNewItemDialog menus={menus} apps={apps} companyTypes={companyTypes} roles={roles} permissions={perms} appId={appId} typeId={typeId} canGrant={canGrant}
                                 onClose={() => setNewItem(false)}
                                 onCreated={async created => { setNewItem(false); toast.success(t('Menu entry created')); await load(); setSelected(created.menuId); }} />
      )}

      {copying && (
        <Modal title={t('Copy layout from…')} onClose={() => setCopying(false)} width={460}>
          <div className="stack">
            <p className="muted-text">{typeId !== null ? t('Starts this company type’s order and visibility from another type’s. The copy goes into the draft; nothing is written until you publish.')
              : t('Starts this application’s order and visibility from another application’s. The copy goes into the draft; nothing is written until you publish.')}</p>
            {copySources.map(src => (
              <div key={src.id} className="menu-row" onClick={() => copyLayout(src.id)}>
                <b className="grow">{src.name}</b>
                <span className="muted-text">{t('{n} entries with their own layout', { n: src.count })}</span>
              </div>
            ))}
          </div>
        </Modal>
      )}

      {publishing && (
        <Modal title={t('Publish the menu')} onClose={() => setPublishing(null)} width={520}
               footer={<><Button text severity="secondary" label={t('Cancel')} onClick={() => setPublishing(null)} disabled={publishing.busy} />
                        <Button icon="pi pi-send" label={t('Publish')} loading={publishing.busy} disabled={!publishing.impact} onClick={() => void publish()} /></>}>
          {!publishing.impact ? <Spinner label={t('Working out what this touches…')} /> : (
            <div className="stack">
              <p>{t('This publishes {n} changes for {scope}. It touches {companies} companies and {users} people, and everything is written or nothing is.', { n: publishing.impact.changes, scope: scopeLabel, companies: publishing.impact.companies, users: publishing.impact.users })}</p>
              <dl className="kv">
                <dt>{t('Hidden')}</dt><dd>{publishing.impact.hidden}</dd>
                <dt>{t('Shown again')}</dt><dd>{publishing.impact.shown}</dd>
                <dt>{t('Reordered')}</dt><dd>{publishing.impact.reordered}</dd>
                <dt>{t('Limits')}</dt><dd>{publishing.impact.limited}</dd>
                <dt>{t('Titles')}</dt><dd>{publishing.impact.titles}</dd>
              </dl>
              <div className="field">
                <span className="field-label">{t('Note for the audit trail')}</span>
                <InputTextarea rows={3} autoResize maxLength={512} value={publishing.note} onChange={e => setPublishing(p => (p ? { ...p, note: e.target.value } : p))} />
              </div>
            </div>
          )}
        </Modal>
      )}
    </>
  );
}

function TitleRow({ language, value, onChange }: { language: { code: string; name: string }; value: string; onChange: (value: string) => void }) {
  const [text, setText] = useState(value);
  useEffect(() => setText(value), [value]);
  return (
    <div className="row" style={{ marginBottom: 6 }}>
      <span style={{ width: 90 }}>{language.name}</span>
      <input className="p-inputtext p-component grow" value={text} onChange={e => setText(e.target.value)} onBlur={() => { if (text !== value) onChange(text); }} />
    </div>
  );
}
