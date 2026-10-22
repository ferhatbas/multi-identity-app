import { useCallback, useEffect, useMemo, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { Button } from 'primereact/button';
import { Dropdown } from 'primereact/dropdown';
import { Badge, EmptyState, PageHeader, SelectField, Spinner } from '@/components/ui/Form';
import { ConfirmDialog, Modal } from '@/components/ui/Modal';
import { useNavigationGuard } from '@/components/ui/useNavigationGuard';
import { useToast } from '@/components/ui/Toast';
import { useAuth } from '@/features/auth/AuthContext';
import {
  DashboardScope, dashboardAdminApi, type DashboardLayoutRead, type DashboardScopeValue, type DashboardView, type DashboardWidgetRead, type Placement,
} from '@/features/dashboard/dashboard.service';
import { definitionsApi } from '@/features/definitions/definitions.service';
import { rolesApi, type RoleRead } from '@/features/roles/roles.service';
import { companiesApi, tenantsApi } from '@/features/tenants/organization.service';
import { UserTypeNames } from '@/types/api';
import { useI18n } from '@/i18n';

const COLUMNS = 24;
const WIDTHS = [6, 8, 12, 16, 24];
const PRECEDENCE: DashboardScopeValue[] = [DashboardScope.User, DashboardScope.Role, DashboardScope.UserType, DashboardScope.Company, DashboardScope.Tenant, DashboardScope.CompanyType, DashboardScope.Application];
const MANAGED: DashboardScopeValue[] = [DashboardScope.Application, DashboardScope.Tenant, DashboardScope.CompanyType, DashboardScope.Company, DashboardScope.Role, DashboardScope.UserType];

interface Item { widget: string; w: number; h: number }
type Named = { id: number; name: string };

function place(items: Item[]): Placement[] {
  const out: Placement[] = [];
  let x = 0, y = 0, rowHeight = 0;
  for (const it of items) {
    if (x + it.w > COLUMNS) { x = 0; y += rowHeight; rowHeight = 0; }
    out.push({ widget: it.widget, x, y, w: it.w, h: it.h });
    x += it.w; rowHeight = Math.max(rowHeight, it.h);
  }
  return out;
}

const parse = (json: string): Item[] => {
  try { return [...(JSON.parse(json) as Placement[])].sort((a, b) => a.y - b.y || a.x - b.x).map(p => ({ widget: p.widget, w: p.w, h: p.h })); } catch { return []; }
};

function Grid({ rows, title }: { rows: Placement[]; title: (code: string) => string }) {
  return (
    <div className="dash-preview" aria-hidden="true">
      {rows.map((p, i) => <div key={i} className="dash-preview-cell" style={{ gridColumn: `${p.x + 1} / span ${p.w}`, gridRow: `${p.y + 1} / span ${Math.max(1, Math.ceil(p.h / 3))}` }}>{title(p.widget)}</div>)}
    </div>
  );
}

export function DashboardManagerPage() {
  const { t } = useI18n();
  const toast = useToast();
  const { user } = useAuth();
  const [params] = useSearchParams();

  const [widgets, setWidgets] = useState<DashboardWidgetRead[]>([]);
  const [layouts, setLayouts] = useState<DashboardLayoutRead[]>([]);
  const [apps, setApps] = useState<Named[]>([]);
  const [tenants, setTenants] = useState<Named[]>([]);
  const [companyTypes, setCompanyTypes] = useState<Named[]>([]);
  const [companies, setCompanies] = useState<Named[]>([]);
  const [roles, setRoles] = useState<RoleRead[]>([]);
  const [loading, setLoading] = useState(true);

  const [appId, setAppId] = useState<number | null>(Number(params.get('applicationTypeId')) || user?.activeApplicationTypeId || null);
  const [scope, setScope] = useState<DashboardScopeValue>((Number(params.get('scope')) as DashboardScopeValue) || DashboardScope.Company);
  const [scopeId, setScopeId] = useState<number | null>(Number(params.get('companyId')) || user?.activeCompanyId || null);
  const [inCompany, setInCompany] = useState<number | null>(null);

  const [items, setItems] = useState<Item[]>([]);
  const [saved, setSaved] = useState('');
  const [saving, setSaving] = useState(false);
  const [removing, setRemoving] = useState(false);
  const [previewing, setPreviewing] = useState(false);
  const [drag, setDrag] = useState<{ kind: 'pool'; code: string } | { kind: 'item'; index: number } | null>(null);

  const load = useCallback(async () => {
    try {
      const [w, l, a, tn, ct, co, ro] = await Promise.all([
        dashboardAdminApi.widgets.list(), dashboardAdminApi.layouts.list(), definitionsApi.applicationTypes.list().catch(() => []), tenantsApi.list().catch(() => []),
        definitionsApi.companyTypes.list().catch(() => []), companiesApi.list().catch(() => []), rolesApi.list().catch(() => []),
      ]);
      setWidgets(w); setLayouts(l); setApps(a); setTenants(tn); setCompanyTypes(ct); setCompanies(co); setRoles(ro);
      setAppId(current => current ?? a[0]?.id ?? null);
    } catch (e) { toast.error((e as Error).message); } finally { setLoading(false); }
  }, []); // eslint-disable-line react-hooks/exhaustive-deps
  useEffect(() => { void load(); }, [load]);

  const usesCompany = scope === DashboardScope.Role || scope === DashboardScope.UserType;
  const targets: Named[] = scope === DashboardScope.Application ? apps
    : scope === DashboardScope.Tenant ? tenants
    : scope === DashboardScope.CompanyType ? companyTypes
    : scope === DashboardScope.Company ? companies
    : scope === DashboardScope.Role ? roles.filter(r => r.applicationTypeId === null || r.applicationTypeId === appId).map(r => ({ id: r.id, name: r.name }))
    : UserTypeNames.slice(0, 4).map((n, i) => ({ id: i, name: n }));
  const targetLabel = (s: DashboardScopeValue) => (s === DashboardScope.Application ? t('Application') : s === DashboardScope.Tenant ? t('Tenant') : s === DashboardScope.CompanyType ? t('Company type')
    : s === DashboardScope.Company ? t('Company') : s === DashboardScope.Role ? t('Role') : s === DashboardScope.UserType ? t('Rank') : t('Person'));
  const listFor = (s: DashboardScopeValue): Named[] => (s === DashboardScope.Application ? apps : s === DashboardScope.Tenant ? tenants : s === DashboardScope.CompanyType ? companyTypes
    : s === DashboardScope.Company ? companies : s === DashboardScope.Role ? roles.map(r => ({ id: r.id, name: r.name })) : []);
  const nameOf = (s: DashboardScopeValue, id: number) => (s === DashboardScope.UserType ? UserTypeNames[id] : listFor(s).find(x => x.id === id)?.name ?? `#${id}`);

  const current = useMemo(() => {
    if (scopeId === null || appId === null) return null;
    const here = layouts.filter(l => l.scope === scope && l.scopeId === scopeId && (usesCompany ? l.companyId === inCompany : l.companyId === null));
    if (scope === DashboardScope.Application) return here.find(l => l.applicationTypeId === null) ?? null;
    return here.find(l => l.applicationTypeId === appId) ?? here.find(l => l.applicationTypeId === null) ?? null;
  }, [layouts, scope, scopeId, inCompany, appId, usesCompany]);

  const inThisApplication = (l: DashboardLayoutRead) => (l.scope === DashboardScope.Application ? l.scopeId === appId : l.applicationTypeId === appId || l.applicationTypeId === null);
  const byCode = useMemo(() => new Map(widgets.map(w => [w.code, w])), [widgets]);
  const titleOf = (code: string) => byCode.get(code)?.title ?? code;

  useEffect(() => {
    const next = current ? parse(current.layoutJson) : [];
    setItems(next); setSaved(JSON.stringify(next));
  }, [current?.id, current?.layoutJson, scope, scopeId, inCompany, appId]); // eslint-disable-line react-hooks/exhaustive-deps

  const dirty = JSON.stringify(items) !== saved;
  useNavigationGuard(dirty, t('There are unsaved changes. Leave anyway?'));

  const guarded = (change: () => void) => { if (dirty && !window.confirm(t('There are unsaved changes. Leave anyway?'))) return; change(); };

  const add = (code: string, at?: number) => {
    const w = byCode.get(code);
    if (!w) return;
    setItems(list => { const it = { widget: code, w: Math.min(COLUMNS, w.defaultWidth || 12), h: w.defaultHeight || 3 }; const c = [...list]; c.splice(at ?? c.length, 0, it); return c; });
  };
  const move = (from: number, to: number) => setItems(list => { if (from === to || to < 0 || to >= list.length) return list; const c = [...list]; const [m] = c.splice(from, 1); c.splice(to, 0, m); return c; });
  const setWidth = (i: number, w: number) => setItems(list => list.map((it, k) => (k === i ? { ...it, w } : it)));
  const removeAt = (i: number) => setItems(list => list.filter((_, k) => k !== i));

  const dropOn = (index: number | null) => {
    if (!drag) return;
    if (drag.kind === 'pool') add(drag.code, index ?? undefined);
    else move(drag.index, index ?? items.length - 1);
    setDrag(null);
  };

  const save = async () => {
    if (scopeId === null || appId === null) return;
    setSaving(true);
    try {
      const body = {
        name: `${targetLabel(scope)} · ${nameOf(scope, scopeId)}${usesCompany && inCompany ? ` · ${companies.find(c => c.id === inCompany)?.name ?? ''}` : ''}`,
        scope, scopeId, applicationTypeId: scope === DashboardScope.Application ? null : appId, companyId: usesCompany ? inCompany : null,
        layoutJson: JSON.stringify(place(items)), isActive: true,
      };
      if (current) await dashboardAdminApi.layouts.update(current.id, body); else await dashboardAdminApi.layouts.create(body);
      toast.success(t('Dashboard saved')); await load();
    } catch (e) { toast.error((e as Error).message); } finally { setSaving(false); }
  };

  const inherit = async () => {
    if (!current) return;
    setSaving(true);
    try { await dashboardAdminApi.layouts.remove(current.id); setRemoving(false); toast.success(t('This layer inherits again')); await load(); }
    catch (e) { toast.error((e as Error).message); } finally { setSaving(false); }
  };

  if (loading) return <Spinner label={t('Loading…')} />;

  const rows = place(items);
  const layerRows = layouts.filter(inThisApplication).sort((a, b) => PRECEDENCE.indexOf(a.scope) - PRECEDENCE.indexOf(b.scope));

  return (
    <>
      <PageHeader title={t('Dashboard Manager')} subtitle={t('Which panels each application, tenant, company type, company, role and rank lands on.')}
                  actions={<>
                    <Button outlined severity="secondary" icon="pi pi-eye" label={t('What would they see?')} disabled={appId === null} onClick={() => setPreviewing(true)} />
                    {current && <Button outlined severity="secondary" icon="pi pi-replay" label={t('Inherit again')} onClick={() => setRemoving(true)} />}
                    <Button icon="pi pi-check" label={t('Save')} loading={saving} disabled={!dirty || scopeId === null} onClick={() => void save()} />
                  </>} />

      <div className="card card-pad row row-wrap" style={{ marginBottom: 14, alignItems: 'flex-end' }}>
        <div style={{ width: 200 }}>
          <SelectField label={t('Application')} value={appId} options={apps.map(a => ({ value: a.id, label: a.name }))} onChange={e => guarded(() => setAppId(e.target.value ? Number(e.target.value) : null))} />
        </div>
        <div style={{ width: 180 }}>
          <SelectField label={t('Layer')} value={scope} options={MANAGED.map(s => ({ value: s, label: targetLabel(s) }))} onChange={e => guarded(() => { setScope(Number(e.target.value) as DashboardScopeValue); setScopeId(null); setInCompany(null); })} />
        </div>
        <div style={{ width: 240 }}>
          <SelectField label={targetLabel(scope)} placeholder="—" value={scopeId} options={targets.map(x => ({ value: x.id, label: x.name }))} onChange={e => guarded(() => setScopeId(e.target.value === '' ? null : Number(e.target.value)))} />
        </div>
        {usesCompany && (
          <div style={{ width: 240 }}>
            <SelectField label={t('Where')} placeholder={t('Across the tenant')} value={inCompany} options={companies.map(x => ({ value: x.id, label: x.name }))} onChange={e => guarded(() => setInCompany(e.target.value ? Number(e.target.value) : null))} />
          </div>
        )}
        {scopeId !== null && (current ? <Badge tone="success">{t('Has its own layout')}</Badge> : <Badge>{t('Inherits')}</Badge>)}
        {dirty && <Badge tone="warning">{t('Unsaved changes')}</Badge>}
      </div>

      <div className="precedence" aria-label={t('Which layer wins')}>
        {PRECEDENCE.map((s, i) => (
          <span key={s} className={`precedence-step${s === scope ? ' is-current' : ''}${s === DashboardScope.User ? ' is-personal' : ''}`}>{i > 0 && <i className="pi pi-angle-right" />}{targetLabel(s)}</span>
        ))}
      </div>

      <div className="studio">
        <div className="card studio-col">
          <div className="card-title" style={{ padding: '12px 14px 0' }}><h3>{t('Widgets')}</h3></div>
          {widgets.length === 0 ? <EmptyState title={t('No widgets')} /> : widgets.map(w => (
            <div key={w.id} className="menu-row" draggable onDragStart={() => setDrag({ kind: 'pool', code: w.code })} onDragEnd={() => setDrag(null)}
                 onClick={() => scopeId !== null && add(w.code)} title={w.description ?? ''}>
              <i className="pi pi-plus" />
              <span className="grow"><b>{w.title}</b> <span className="muted-text">· {w.code}</span></span>
              <span className="muted-text">{w.defaultWidth}×{w.defaultHeight}</span>
            </div>
          ))}
        </div>

        <div className="card studio-col" onDragOver={e => { if (drag) e.preventDefault(); }} onDrop={e => { e.preventDefault(); dropOn(null); }}>
          <div className="card-title" style={{ padding: '12px 14px 0' }}><h3>{t('Layout')}</h3></div>
          {scopeId === null ? <EmptyState title={t('Choose a layer')} /> : items.length === 0 ? <EmptyState title={t('Nothing here yet')} text={t('Drag a widget here, or click one on the left.')} /> : (
            <div style={{ padding: '0 10px' }}>
              {items.map((it, i) => (
                <div key={`${it.widget}-${i}`} className={`menu-row${drag?.kind === 'item' && drag.index === i ? ' is-dragging' : ''}`} style={{ cursor: 'grab' }} draggable
                     onDragStart={() => setDrag({ kind: 'item', index: i })} onDragEnd={() => setDrag(null)}
                     onDragOver={e => { if (drag) { e.preventDefault(); e.stopPropagation(); } }} onDrop={e => { e.preventDefault(); e.stopPropagation(); dropOn(i); }}>
                  <i className="pi pi-bars drag-handle" aria-hidden="true" />
                  <span className="grow"><b>{titleOf(it.widget)}</b> <span className="muted-text">· {rows[i].x},{rows[i].y}</span></span>
                  <Dropdown value={it.w} aria-label={t('Width')} appendTo="self" style={{ width: 96 }} options={(WIDTHS.includes(it.w) ? WIDTHS : [...WIDTHS, it.w].sort((x, y) => x - y)).map(w => ({ label: `${w}/24`, value: w }))}
                            onChange={e => setWidth(i, e.value as number)} />
                  <Button text rounded size="small" severity="secondary" icon="pi pi-arrow-up" aria-label={t('Move up')} disabled={i === 0} onClick={() => move(i, i - 1)} />
                  <Button text rounded size="small" severity="secondary" icon="pi pi-arrow-down" aria-label={t('Move down')} disabled={i === items.length - 1} onClick={() => move(i, i + 1)} />
                  <Button text rounded size="small" severity="danger" icon="pi pi-times" aria-label={t('Remove')} onClick={() => removeAt(i)} />
                </div>
              ))}
              <Grid rows={rows} title={titleOf} />
            </div>
          )}
        </div>

        <div className="card studio-col">
          <div className="card-title" style={{ padding: '12px 14px 0' }}><h3>{t('Layers with their own layout')}</h3></div>
          {layerRows.length === 0 ? <EmptyState title={t('None yet')} /> : layerRows.map(l => (
            <div key={l.id} className={`menu-row${current?.id === l.id ? ' is-selected' : ''}`}
                 onClick={() => guarded(() => { setScope(l.scope); setScopeId(l.scopeId); setInCompany(l.companyId); })}>
              <span className="grow"><b>{nameOf(l.scope, l.scopeId)}</b>{l.companyId ? <span className="muted-text"> · {companies.find(c => c.id === l.companyId)?.name}</span> : null}</span>
              <Badge>{targetLabel(l.scope)}</Badge>
            </div>
          ))}
        </div>
      </div>

      {previewing && appId !== null && <PreviewDialog appId={appId} companies={companies} roles={roles.filter(r => r.applicationTypeId === null || r.applicationTypeId === appId)} titleOf={titleOf} nameOf={nameOf} onClose={() => setPreviewing(false)} />}
      {removing && <ConfirmDialog title={t('Inherit again?')} message={t('This layer’s own layout is deleted and it shows the dashboard of the layer above it.')} confirmLabel={t('Inherit again')} danger busy={saving} onCancel={() => setRemoving(false)} onConfirm={() => void inherit()} />}
    </>
  );
}

function PreviewDialog({ appId, companies, roles, titleOf, nameOf, onClose }: {
  appId: number; companies: Named[]; roles: RoleRead[]; titleOf: (code: string) => string; nameOf: (s: DashboardScopeValue, id: number) => string; onClose: () => void;
}) {
  const { t } = useI18n();
  const toast = useToast();
  const [companyId, setCompanyId] = useState<number | null>(companies[0]?.id ?? null);
  const [roleId, setRoleId] = useState<number | null>(null);
  const [rank, setRank] = useState<number | null>(1);
  const [view, setView] = useState<DashboardView | null>(null);

  useEffect(() => {
    setView(null);
    dashboardAdminApi.preview({ applicationTypeId: appId, companyId, roleId, userType: rank }).then(setView).catch(e => toast.error((e as Error).message));
  }, [appId, companyId, roleId, rank]); // eslint-disable-line react-hooks/exhaustive-deps

  const sourceName: Record<string, string> = {
    user: t('Their own dashboard'), roleInCompany: t('Role in their company'), role: t('Role across the tenant'), userTypeInCompany: t('Rank in their company'), userType: t('Rank across the tenant'),
    company: t('Company'), tenant: t('Tenant'), companyType: t('Company type'), application: t('Application'), default: t('Every widget they may see'),
  };
  const source = view && view.sourceScope !== undefined && view.sourceScope !== null ? `${sourceName[view.source] ?? view.source} · ${nameOf(view.sourceScope, view.sourceScopeId ?? 0)}` : sourceName.default;

  return (
    <Modal title={t('What would they see?')} onClose={onClose} width={760} footer={<Button label={t('Close')} onClick={onClose} />}>
      <div className="stack">
        <div className="row row-wrap" style={{ alignItems: 'flex-end' }}>
          <div style={{ width: 200 }}><SelectField label={t('Company')} placeholder="—" value={companyId} options={companies.map(c => ({ value: c.id, label: c.name }))} onChange={e => setCompanyId(e.target.value ? Number(e.target.value) : null)} /></div>
          <div style={{ width: 180 }}><SelectField label={t('Role')} placeholder={t('Nobody')} value={roleId} options={roles.map(r => ({ value: r.id, label: r.name }))} onChange={e => setRoleId(e.target.value ? Number(e.target.value) : null)} /></div>
          <div style={{ width: 160 }}><SelectField label={t('Rank')} placeholder="—" value={rank} options={UserTypeNames.slice(0, 4).map((n, i) => ({ value: i, label: n }))} onChange={e => setRank(e.target.value === '' ? null : Number(e.target.value))} /></div>
        </div>
        {!view ? <Spinner label={t('Loading…')} /> : (
          <>
            <div className="muted-text">{t('Comes from')}: <b>{source}</b>{view.hiddenCount ? ` · ${t('{n} panels hidden by permissions', { n: view.hiddenCount })}` : ''}</div>
            {view.items.length === 0 ? <EmptyState title={t('Nothing for this person')} /> : <Grid rows={view.items.map(i => ({ widget: i.widget, x: i.x, y: i.y, w: i.w, h: i.h }))} title={titleOf} />}
          </>
        )}
      </div>
    </Modal>
  );
}
