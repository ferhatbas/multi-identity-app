import { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { Button } from 'primereact/button';
import { PickList } from 'primereact/picklist';
import { CheckField, EmptyState, PageHeader, SelectField, Spinner, TextArea, TextField } from '@/components/ui/Form';
import { TabBar } from '@/components/ui/Tabs';
import { useToast } from '@/components/ui/Toast';
import { useAuth } from '@/features/auth/AuthContext';
import { P } from '@/features/auth/permissions';
import { useOptions } from '@/features/lookups/useOptions';
import { rolesApi, type PermissionRead, type RolePermissionRead, type RoleRead } from '@/features/roles/roles.service';
import { ApiException } from '@/services/http/httpService';
import { useI18n } from '@/i18n';

interface Form { name: string; code: string; description: string; applicationTypeId: number | null; isActive: boolean }

const toForm = (r: RoleRead): Form => ({
  name: r.name, code: r.code ?? '', description: r.description ?? '', applicationTypeId: r.applicationTypeId, isActive: r.isActive,
});

export function RoleEditorPage() {
  const { t } = useI18n();
  const toast = useToast();
  const navigate = useNavigate();
  const { can } = useAuth();
  const roleId = Number(useParams().id);
  const canWrite = can(P.RoleManage);

  const [tab, setTab] = useState<'general' | 'permissions'>('general');
  const [role, setRole] = useState<RoleRead | null>(null);
  const [form, setForm] = useState<Form | null>(null);
  const [permissions, setPermissions] = useState<PermissionRead[]>([]);
  const [grants, setGrants] = useState<RolePermissionRead[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const apps = useOptions(() => rolesApi.applications.list(), r => ({ value: r.id, label: r.name }));

  const load = useCallback(async () => {
    try {
      const [r, all, g] = await Promise.all([rolesApi.get(roleId), rolesApi.permissions.list(), rolesApi.grants.list()]);
      setRole(r);
      setForm(toForm(r));
      setPermissions(all.filter(p => !p.isManagement));
      setGrants(g.filter(x => x.roleId === roleId));
    } catch (e) {
      toast.error((e as Error).message);
      setRole(null);
    } finally {
      setLoading(false);
    }
  }, [roleId, toast]);
  useEffect(() => { void load(); }, [load]);

  const dirty = !!role && !!form && JSON.stringify(form) !== JSON.stringify(toForm(role));
  const grantedIds = useMemo(() => new Set(grants.map(g => g.permissionId)), [grants]);
  const available = permissions.filter(p => !grantedIds.has(p.id));
  const granted = permissions.filter(p => grantedIds.has(p.id));

  const set = <K extends keyof Form>(key: K, value: Form[K]) => setForm(f => (f ? { ...f, [key]: value } : f));

  const save = async () => {
    if (!role || !form) return;
    setSaving(true);
    setErrors({});
    try {
      const body = { name: form.name, code: form.code.trim() || null, description: form.description.trim() || null, applicationTypeId: form.applicationTypeId, isActive: form.isActive };
      const updated = await rolesApi.update(role.id, body);
      setRole(updated);
      setForm(toForm(updated));
      toast.success(t('Role saved'));
    } catch (e) {
      if (e instanceof ApiException) {
        const next: Record<string, string> = {};
        for (const f of ['name', 'code', 'description']) { const m = e.fieldMessage(f); if (m) next[f] = m; }
        setErrors(next);
        if (Object.keys(next).length === 0) toast.error(e.message);
      } else toast.error((e as Error).message);
    } finally {
      setSaving(false);
    }
  };

  const applyGranted = async (next: PermissionRead[]) => {
    if (!role) return;
    const nextIds = new Set(next.map(p => p.id));
    const toAdd = [...nextIds].filter(id => !grantedIds.has(id));
    const toRemove = grants.filter(g => !nextIds.has(g.permissionId));
    try {
      for (const id of toAdd) {
        const created = await rolesApi.grants.create({ roleId: role.id, permissionId: id });
        setGrants(list => [...list, created]);
      }
      for (const g of toRemove) {
        await rolesApi.grants.remove(g.id);
        setGrants(list => list.filter(x => x.id !== g.id));
      }
    } catch (e) {
      toast.error((e as Error).message);
      void load();
    }
  };

  if (loading) return <Spinner label={t('Loading…')} />;
  if (!role || !form) return <EmptyState title={t('Role not found')} />;

  return (
    <>
      <PageHeader
        title={role.name} subtitle={role.description ?? ''}
        actions={<Button outlined severity="secondary" icon="pi pi-arrow-left" label={t('Back')} onClick={() => navigate('/admin/roles')} />}
      />
      <TabBar tabs={[{ id: 'general', label: t('General') }, { id: 'permissions', label: t('Permissions') }]} value={tab} onChange={setTab} />

      {tab === 'general' && (
        <div className="card card-pad" style={{ maxWidth: 760 }}>
          <div className="form-grid">
            <TextField label={t('Name')} required maxLength={256} value={form.name} error={errors.name} disabled={!canWrite} onChange={e => set('name', e.target.value)} />
            <TextField label={t('Code')} maxLength={64} value={form.code} error={errors.code} disabled={!canWrite} onChange={e => set('code', e.target.value)} />
            <TextArea label={t('Description')} wide maxLength={512} value={form.description} disabled={!canWrite} onChange={e => set('description', e.target.value)} />
            <SelectField label={t('Application')} placeholder={t('Shared (all applications)')} options={apps.options} disabled={!canWrite}
                         value={form.applicationTypeId} hint={t('A role tied to an application can only be used in companies of that application.')}
                         onChange={e => set('applicationTypeId', e.target.value === '' ? null : Number(e.target.value))} />
            <div className="field-wide"><CheckField label={t('Active')} checked={form.isActive} disabled={!canWrite} onChange={v => set('isActive', v)} /></div>
          </div>
        </div>
      )}

      {tab === 'permissions' && (
        <div className="stack">
          <p className="muted-text">{t('{granted} of {total} permissions granted. Moves are saved at once.', { granted: granted.length, total: permissions.length })}</p>
          <div className="alert alert-info">{t('Management permissions come with a person’s rank and cannot be given through a role.')}</div>
          <PickList
            dataKey="id" source={available} target={granted} filter filterBy="name,code" sourceFilterPlaceholder={t('Search')} targetFilterPlaceholder={t('Search')}
            sourceHeader={t('Available')} targetHeader={t('Granted')} showSourceControls={false} showTargetControls={false}
            sourceStyle={{ height: 420 }} targetStyle={{ height: 420 }}
            itemTemplate={(p: PermissionRead) => <div><div className="cell-main">{p.name}</div><div className="cell-sub"><code>{p.code}</code></div></div>}
            onChange={e => { if (canWrite) void applyGranted(e.target as PermissionRead[]); }}
          />
          {!canWrite && <div className="alert alert-warning">{t('You may look at this role but not change it.')}</div>}
        </div>
      )}

      {tab === 'general' && canWrite && (
        <div className="save-bar" role="region" aria-label={t('Save')}>
          <span className="muted-text">{dirty ? t('You have unsaved changes.') : t('Nothing to save.')}</span>
          <Button text severity="secondary" label={t('Discard')} disabled={!dirty || saving} onClick={() => { setForm(toForm(role)); setErrors({}); }} />
          <Button label={saving ? t('Saving…') : t('Save')} disabled={!dirty || !form.name.trim()} loading={saving} onClick={() => void save()} />
        </div>
      )}
    </>
  );
}
