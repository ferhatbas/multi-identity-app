import { useCallback, useEffect, useMemo, useState } from 'react';
import { Button } from 'primereact/button';
import { InputSwitch } from 'primereact/inputswitch';
import { Badge, CheckField, EmptyState, SelectField, Spinner } from '@/components/ui/Form';
import { Icon } from '@/components/ui/Icon';
import { ConfirmDialog } from '@/components/ui/Modal';
import { SearchBox } from '@/components/ui/DataTable';
import { useToast } from '@/components/ui/Toast';
import { useAuth } from '@/features/auth/AuthContext';
import { P } from '@/features/auth/permissions';
import { rolesApi, type PermissionRead, type RoleRead } from '@/features/roles/roles.service';
import type { CompanyRead } from '@/features/tenants/organization.service';
import { unitsApi, type UnitRead, type UserUnitRead } from '@/features/units/units.service';
import {
  usersApi, type EffectivePermissions, type UserCompanyRead, type UserCompanyRoleRead, type UserPermissionOverride, type UserRead,
} from '@/features/users/users.service';
import { UserType, UserTypeNames } from '@/types/api';
import { useI18n } from '@/i18n';

interface TabProps { user: UserRead; companies: CompanyRead[]; memberships: UserCompanyRead[]; onChanged: () => void }

const companyName = (companies: CompanyRead[], id: number) => companies.find(c => c.id === id)?.name ?? `#${id}`;

export function CompaniesTab({ user, companies, memberships, onChanged }: TabProps) {
  const { t } = useI18n();
  const toast = useToast();
  const { can } = useAuth();
  const canEdit = can(P.UserManage);
  const [pick, setPick] = useState('');
  const [removing, setRemoving] = useState<UserCompanyRead | null>(null);
  const [busy, setBusy] = useState(false);
  const free = companies.filter(c => !memberships.some(m => m.companyId === c.id));

  const run = async (work: () => Promise<unknown>, done: string) => {
    setBusy(true);
    try { await work(); toast.success(done); onChanged(); } catch (e) { toast.error((e as Error).message); } finally { setBusy(false); }
  };

  return (
    <div className="stack" style={{ maxWidth: 760 }}>
      <p className="muted-text">{t('{n} companies', { n: memberships.length })}</p>
      {memberships.length === 0 && <EmptyState title={t('Not a member of any company')} />}
      {memberships.map(m => {
        const company = companies.find(c => c.id === m.companyId);
        return (
          <div key={m.id} className="card card-pad row">
            <Icon name="building" size={20} />
            <div className="grow"><div className="cell-main">{companyName(companies, m.companyId)}</div><div className="cell-sub">{company?.code ?? ''}</div></div>
            {m.companyId === user.activeCompanyId && <Badge tone="info">{t('Active')}</Badge>}
            {canEdit && <Button text rounded severity="danger" icon="pi pi-trash" aria-label={t('Remove from company')} onClick={() => setRemoving(m)} />}
          </div>
        );
      })}
      {canEdit && free.length > 0 && (
        <div className="row row-wrap" style={{ alignItems: 'flex-end' }}>
          <SelectField label={t('Add company')} value={pick} onChange={e => setPick(e.target.value)} placeholder="—" options={free.map(c => ({ value: c.id, label: c.name }))} />
          <Button label={t('Add company')} icon="pi pi-plus" disabled={!pick || busy} loading={busy}
                  onClick={() => void run(async () => { await usersApi.memberships.create({ userId: user.id, companyId: Number(pick), membershipType: UserType.Standard }); setPick(''); }, t('Company added'))} />
        </div>
      )}
      {removing && (
        <ConfirmDialog title={t('Remove from company')} message={t('{company} — remove?', { company: companyName(companies, removing.companyId) })} confirmLabel={t('Remove')} danger busy={busy}
                       onCancel={() => setRemoving(null)} onConfirm={() => void run(() => usersApi.memberships.remove(removing.id), t('Company removed')).then(() => setRemoving(null))} />
      )}
    </div>
  );
}

export function UserTypeTab({ user, companies, memberships, onChanged }: TabProps) {
  const { t } = useI18n();
  const toast = useToast();
  const { user: me, can } = useAuth();
  const [busy, setBusy] = useState<number | null>(null);
  const callerRank = (company: number) => me?.isPlatformAdmin ? UserType.PlatformAdmin : (me?.companies.find(c => c.companyId === company)?.membershipType ?? UserType.Reader);
  const canEdit = can(P.UserManage);

  const change = async (m: UserCompanyRead, rank: number) => {
    setBusy(m.id);
    try {
      await usersApi.memberships.update(m.id, { userId: m.userId, companyId: m.companyId, membershipType: rank });
      toast.success(t('Rank saved'));
      onChanged();
    } catch (e) { toast.error((e as Error).message); } finally { setBusy(null); }
  };

  return (
    <div className="stack" style={{ maxWidth: 760 }}>
      <p className="muted-text">{t('The rank decides what administration a person has in a company. It is separate from their roles, which are about the work.')}</p>
      <div className="card card-pad row">
        <Icon name="globe" size={20} />
        <div className="grow"><div className="cell-main">{t('Platform administrator')}</div><div className="cell-sub">{t('Works across every tenant. Set when the account is created.')}</div></div>
        <InputSwitch checked={user.isPlatformAdmin} disabled />
      </div>
      {memberships.length === 0 && <EmptyState title={t('Not a member of any company')} />}
      {memberships.map(m => {
        const above = callerRank(m.companyId);
        return (
          <div key={m.id} className="card card-pad row">
            <div className="grow cell-main">{companyName(companies, m.companyId)}</div>
            <div style={{ width: 240 }}>
              <SelectField label="" value={m.membershipType} disabled={!canEdit || busy === m.id}
                           options={UserTypeNames.slice(0, 4).map((n, i) => ({ value: i, label: n })).filter(o => o.value < above || o.value === m.membershipType)}
                           onChange={e => void change(m, Number(e.target.value))} />
            </div>
          </div>
        );
      })}
    </div>
  );
}

export function RolesTab({ user, companies, memberships, roles, onChanged }: TabProps & { roles: RoleRead[] }) {
  const { t } = useI18n();
  const toast = useToast();
  const { can } = useAuth();
  const canEdit = can(P.UserManage);
  const [grants, setGrants] = useState<UserCompanyRoleRead[] | null>(null);
  const [busy, setBusy] = useState<string | null>(null);
  const apps = useMemo(() => new Map<number, string>(), []);
  const [appNames, setAppNames] = useState(apps);

  const load = useCallback(() => { usersApi.roleGrants.list().then(g => setGrants(g.filter(x => x.userId === user.id))).catch((e: Error) => toast.error(e.message)); }, [user.id, toast]);
  useEffect(load, [load]);
  useEffect(() => { rolesApi.applications.list().then(a => setAppNames(new Map(a.map(x => [x.id, x.name])))).catch(() => undefined); }, []);

  const toggle = async (companyId: number, role: RoleRead) => {
    const held = grants?.find(g => g.companyId === companyId && g.roleId === role.id);
    setBusy(`${companyId}-${role.id}`);
    try {
      if (held) await usersApi.roleGrants.remove(held.id);
      else await usersApi.roleGrants.create({ userId: user.id, companyId, roleId: role.id });
      load();
      onChanged();
    } catch (e) { toast.error((e as Error).message); } finally { setBusy(null); }
  };

  if (!grants) return <Spinner label={t('Loading…')} />;
  if (memberships.length === 0) return <EmptyState title={t('Not a member of any company')} text={t('Add the person to a company first.')} />;

  return (
    <div className="stack" style={{ maxWidth: 900 }}>
      <p className="muted-text">{t('Roles the person holds in each company. A click gives or takes a role at once.')}</p>
      {memberships.map(m => {
        const held = grants.filter(g => g.companyId === m.companyId);
        return (
          <div key={m.id} className={`card card-pad${m.companyId === user.activeCompanyId ? ' card-active' : ''}`}>
            <div className="row" style={{ marginBottom: 10 }}>
              <Icon name="building" size={18} /><strong>{companyName(companies, m.companyId)}</strong>
              {m.companyId === user.activeCompanyId && <span className="muted-text">({t('Active')})</span>}
            </div>
            {roles.length === 0 && <div className="alert alert-warning">{t('No roles defined yet.')}</div>}
            <div className="row row-wrap" style={{ gap: 8 }}>
              {roles.map(r => {
                const on = held.some(g => g.roleId === r.id);
                return (
                  <Button key={r.id} size="small" outlined={!on} severity={on ? 'success' : 'secondary'} disabled={!canEdit || busy === `${m.companyId}-${r.id}`}
                          label={`${on ? '✓' : '+'} ${r.name}${r.applicationTypeId ? ` · ${appNames.get(r.applicationTypeId) ?? ''}` : ''}`} onClick={() => void toggle(m.companyId, r)} />
                );
              })}
            </div>
          </div>
        );
      })}
    </div>
  );
}

export function UnitsTab({ user, onChanged }: Pick<TabProps, 'user' | 'onChanged'>) {
  const { t } = useI18n();
  const toast = useToast();
  const { can } = useAuth();
  const canEdit = can(P.UnitManage);
  const [units, setUnits] = useState<UnitRead[]>([]);
  const [userUnits, setUserUnits] = useState<UserUnitRead[] | null>(null);
  const [pick, setPick] = useState('');
  const [primary, setPrimary] = useState(false);
  const [busy, setBusy] = useState(false);

  const load = useCallback(async () => {
    const [all, members] = await Promise.all([unitsApi.list(), unitsApi.members.list()]);
    setUnits(all);
    setUserUnits(members.filter(m => m.userId === user.id));
  }, [user.id]);
  useEffect(() => { load().catch((e: Error) => toast.error(e.message)); }, [load, toast]);

  const run = async (work: () => Promise<unknown>) => {
    setBusy(true);
    try { await work(); await load(); onChanged(); } catch (e) { toast.error((e as Error).message); } finally { setBusy(false); }
  };

  if (!userUnits) return <Spinner label={t('Loading…')} />;
  const free = units.filter(u => !userUnits.some(m => m.unitId === u.id));
  const nameOf = (id: number) => units.find(u => u.id === id)?.name ?? `#${id}`;

  return (
    <div className="stack" style={{ maxWidth: 760 }}>
      <p className="muted-text">{t('A person shows as staff of the units they are assigned to.')}</p>
      {canEdit && (
        <div className="row row-wrap" style={{ alignItems: 'flex-end' }}>
          <SelectField label={t('Unit')} value={pick} placeholder={t('Select a unit')} onChange={e => setPick(e.target.value)} options={free.map(u => ({ value: u.id, label: `${u.name} (${u.code})` }))} />
          <CheckField label={t('Primary unit')} checked={primary} onChange={setPrimary} />
          <Button label={t('Add')} icon="pi pi-plus" disabled={!pick || busy} onClick={() => void run(async () => { await unitsApi.members.create({ userId: user.id, unitId: Number(pick), isPrimary: primary }); setPick(''); setPrimary(false); })} />
        </div>
      )}
      {userUnits.length === 0 && <EmptyState title={t('No unit assigned')} text={t('Select a unit above and add it.')} />}
      {userUnits.map(m => (
        <div key={m.id} className="card card-pad row">
          <Icon name="tree" size={18} /><strong className="grow">{nameOf(m.unitId)}</strong>
          {m.isPrimary && <Badge tone="info">{t('Primary')}</Badge>}
          {canEdit && !m.isPrimary && <Button text size="small" label={t('Make primary')} onClick={() => void run(() => unitsApi.members.update(m.id, { userId: user.id, unitId: m.unitId, isPrimary: true }))} />}
          {canEdit && <Button text rounded severity="danger" icon="pi pi-times" aria-label={t('Remove')} onClick={() => void run(() => unitsApi.members.remove(m.id))} />}
        </div>
      ))}
    </div>
  );
}

type State = 'inherit' | 'grant' | 'deny';

export function PermissionsTab({ user, companies, memberships }: TabProps) {
  const { t } = useI18n();
  const toast = useToast();
  const { can } = useAuth();
  const [companyId, setCompanyId] = useState<number | null>(user.activeCompanyId ?? memberships[0]?.companyId ?? null);
  const [permissions, setPermissions] = useState<PermissionRead[]>([]);
  const [effective, setEffective] = useState<EffectivePermissions | null>(null);
  const [overrides, setOverrides] = useState<UserPermissionOverride[]>([]);
  const [search, setSearch] = useState('');
  const [busy, setBusy] = useState<number | null>(null);
  const membership = memberships.find(m => m.companyId === companyId);
  const editable = can(P.UserManage) && !user.isPlatformAdmin;

  const load = useCallback(async () => {
    if (companyId === null) return;
    const [all, eff, over] = await Promise.all([rolesApi.permissions.list(), usersApi.effectivePermissions(user.id, companyId), usersApi.permissionOverrides.list()]);
    setPermissions(all);
    setEffective(eff);
    setOverrides(over.filter(o => o.userId === user.id && o.companyId === companyId));
  }, [user.id, companyId]);
  useEffect(() => { setEffective(null); load().catch((e: Error) => toast.error(e.message)); }, [load, toast]);

  if (memberships.length === 0 || companyId === null) return <EmptyState title={t('Not a member of any company')} />;

  const stateOf = (p: PermissionRead): State => { const o = overrides.find(x => x.permissionId === p.id); return !o ? 'inherit' : o.isGranted ? 'grant' : 'deny'; };
  const set = async (p: PermissionRead, state: State) => {
    const existing = overrides.find(o => o.permissionId === p.id);
    setBusy(p.id);
    try {
      if (state === 'inherit') { if (existing) await usersApi.permissionOverrides.remove(existing.id); }
      else if (existing) await usersApi.permissionOverrides.update(existing.id, { userId: user.id, companyId, permissionId: p.id, isGranted: state === 'grant' });
      else await usersApi.permissionOverrides.create({ userId: user.id, companyId, permissionId: p.id, isGranted: state === 'grant' });
      await load();
    } catch (e) { toast.error((e as Error).message); } finally { setBusy(null); }
  };

  const shown = permissions.filter(p => `${p.name} ${p.code}`.toLowerCase().includes(search.toLowerCase()));
  const via = (code: string) => effective?.granted.find(g => g.code.toLowerCase() === code.toLowerCase())?.via ?? [];

  return (
    <div className="stack" style={{ maxWidth: 900 }}>
      <div className="alert alert-info">{t('What this person may do in the company, and where each permission comes from. A personal override wins over the roles.')}</div>
      {user.isPlatformAdmin && <div className="alert alert-warning">{t('A platform administrator holds every permission; overrides do not apply.')}</div>}
      {membership?.membershipType === UserType.Reader && <div className="alert alert-warning">{t('A reader cannot change anything, whatever they are granted.')}</div>}
      <div className="toolbar" style={{ marginBottom: 0 }}>
        <div style={{ width: 260 }}>
          <SelectField label="" value={companyId} options={memberships.map(m => ({ value: m.companyId, label: companyName(companies, m.companyId) }))} onChange={e => setCompanyId(Number(e.target.value))} />
        </div>
        <SearchBox onChange={setSearch} />
      </div>
      {!effective ? <Spinner label={t('Loading…')} /> : shown.map(p => {
        const state = stateOf(p);
        const allowed = effective.granted.some(g => g.code.toLowerCase() === p.code.toLowerCase());
        return (
          <div key={p.id} className="card card-pad row row-wrap">
            <div className="grow">
              <div className="cell-main">{p.name}</div>
              <div className="cell-sub"><code>{p.code}</code></div>
              <div className="row row-wrap" style={{ gap: 6, marginTop: 4 }}>
                {via(p.code).length > 0 && <Badge tone="info">{t('Via')}: {via(p.code).join(', ')}</Badge>}
                {allowed ? <Badge tone="success">{t('Allowed')}</Badge> : state === 'deny' ? <Badge tone="danger">{t('Denied')}</Badge> : null}
                {p.isManagement && <Badge tone="warning">{t('Management')}</Badge>}
              </div>
            </div>
            <div className="row" style={{ gap: 4 }}>
              {(['inherit', 'grant', 'deny'] as State[]).map(s => (
                <Button key={s} size="small" outlined={state !== s} severity={s === 'grant' ? 'success' : s === 'deny' ? 'danger' : 'secondary'}
                        label={s === 'inherit' ? t('Inherit') : s === 'grant' ? t('Grant') : t('Deny')}
                        disabled={!editable || busy === p.id || (p.isManagement && s !== 'inherit')} onClick={() => void set(p, s)} />
              ))}
            </div>
          </div>
        );
      })}
    </div>
  );
}
