import { useEffect, useMemo, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { Button } from 'primereact/button';
import { TabPanel, TabView } from 'primereact/tabview';
import type { MenuItem } from 'primereact/menuitem';
import { DataTable, SearchBox, exportCsv, usePagedTable, type Column } from '@/components/ui/DataTable';
import { Badge, CheckField, EmptyState, PageHeader, SelectField, Spinner, TextField } from '@/components/ui/Form';
import { ConfirmDialog } from '@/components/ui/Modal';
import { Avatar, RowMenu } from '@/components/ui/RowMenu';
import { SaveBar } from '@/components/ui/SaveBar';
import { useToast } from '@/components/ui/Toast';
import { useAuth } from '@/features/auth/AuthContext';
import { P } from '@/features/auth/permissions';
import { useOptions } from '@/features/lookups/useOptions';
import { companiesApi, languagesApi, tenantsApi, type CompanyRead, type TenantRead } from '@/features/tenants/organization.service';
import { ApiException } from '@/services/http/httpService';
import { useI18n } from '@/i18n';

export function TenantsListPage() {
  const { t, formatDate } = useI18n();
  const toast = useToast();
  const navigate = useNavigate();
  const { can } = useAuth();
  const canWrite = can(P.TenantManage);
  const table = usePagedTable<TenantRead>(tenantsApi.paged, { orderBy: 'name', pageSize: 12 });
  const [deleting, setDeleting] = useState<TenantRead | null>(null);
  const [busy, setBusy] = useState(false);

  const remove = async () => {
    if (!deleting) return;
    setBusy(true);
    try { await tenantsApi.remove(deleting.id); toast.success(t('Tenant deleted')); setDeleting(null); table.reload(); }
    catch (e) { toast.error((e as Error).message); } finally { setBusy(false); }
  };

  const columns = useMemo<Column<TenantRead>[]>(() => [
    { key: 'name', header: t('Tenant'), sortKey: 'name', csv: r => r.name, render: r => <div className="row"><Avatar id={r.id} text={r.name} /><span className="cell-main">{r.name}</span></div> },
    { key: 'code', header: t('Code'), sortKey: 'code', csv: r => r.code, render: r => <code>{r.code || '—'}</code> },
    { key: 'status', header: t('Status'), csv: r => r.isActive, render: r => (r.isActive ? <Badge tone="success">{t('Active')}</Badge> : <Badge>{t('Inactive')}</Badge>) },
    { key: 'created', header: t('Created'), sortKey: 'createdAt', csv: r => r.createdAt, render: r => formatDate(r.createdAt, false) },
  ], [t, formatDate]);

  const menu = (r: TenantRead): MenuItem[] => [
    { label: t('Edit'), icon: 'pi pi-pencil', command: () => navigate(`/admin/tenants/${r.id}`) },
    ...(canWrite ? [{ separator: true }, { label: t('Delete'), icon: 'pi pi-trash', className: 'menu-danger', command: () => setDeleting(r) }] : []),
  ];

  return (
    <>
      <PageHeader title={t('Tenants')} subtitle={t('The customers that own companies and people.')}
                  actions={canWrite && <Button icon="pi pi-plus" label={t('New tenant')} onClick={() => navigate('/admin/tenants/new')} />} />
      <div className="toolbar">
        <SearchBox onChange={table.setSearch} placeholder={t('Search by name or code…')} />
        <div className="grow" />
        <Button size="small" outlined severity="secondary" icon="pi pi-download" label={t('Export CSV')} disabled={table.rows.length === 0} onClick={() => exportCsv(columns, table.rows, 'tenants.csv')} />
        <Button text severity="secondary" icon="pi pi-refresh" aria-label={t('Refresh')} onClick={table.reload} />
      </div>
      <DataTable columns={columns} table={table} rowKey={r => r.id} rowClass={r => (r.isActive ? undefined : 'row-muted')} onRowClick={r => navigate(`/admin/tenants/${r.id}`)}
                 emptyTitle={t('No tenants yet')}
                 actions={r => <><Button text rounded severity="secondary" icon="pi pi-pencil" aria-label={t('Edit')} onClick={() => navigate(`/admin/tenants/${r.id}`)} /><RowMenu items={menu(r)} /></>} />
      {deleting && <ConfirmDialog title={t('Delete tenant')} message={t('Delete {name}? It can be restored later.', { name: deleting.name })} confirmLabel={t('Delete')} danger busy={busy} onCancel={() => setDeleting(null)} onConfirm={() => void remove()} />}
    </>
  );
}

export function TenantCreatePage() {
  const { t } = useI18n();
  const toast = useToast();
  const navigate = useNavigate();
  const [name, setName] = useState('');
  const [code, setCode] = useState('');
  const [busy, setBusy] = useState(false);
  const [errors, setErrors] = useState<Record<string, string>>({});

  const create = async () => {
    setBusy(true);
    setErrors({});
    try {
      const tenant = await tenantsApi.create({ name: name.trim(), code: code.trim().toUpperCase(), isActive: true });
      toast.success(t('Tenant created'));
      navigate(`/admin/tenants/${tenant.id}`, { replace: true });
    } catch (e) {
      if (e instanceof ApiException) {
        const next: Record<string, string> = {};
        for (const f of ['name', 'code']) { const m = e.fieldMessage(f); if (m) next[f] = m; }
        setErrors(next);
        if (Object.keys(next).length === 0) toast.error(e.message);
      } else toast.error((e as Error).message);
    } finally { setBusy(false); }
  };

  return (
    <>
      <PageHeader title={t('New tenant')} />
      <div className="card card-pad" style={{ maxWidth: 640 }}>
        <div className="form-grid">
          <TextField label={t('Tenant name')} required value={name} error={errors.name} placeholder={t('e.g. Alpha Group')} onChange={e => setName(e.target.value)} />
          <TextField label={t('Tenant code')} required value={code} error={errors.code} maxLength={32} style={{ textTransform: 'uppercase' }} hint={t('Short and unique; upper case.')} onChange={e => setCode(e.target.value.toUpperCase())} />
        </div>
        <div className="row" style={{ justifyContent: 'flex-end', marginTop: 18 }}>
          <Button text severity="secondary" label={t('Cancel')} onClick={() => navigate('/admin/tenants')} disabled={busy} />
          <Button icon="pi pi-check" label={t('Create')} disabled={!name.trim() || !code.trim() || busy} loading={busy} onClick={() => void create()} />
        </div>
      </div>
    </>
  );
}

interface Form { name: string; code: string; languageCode: string; isActive: boolean }
const toForm = (r: TenantRead): Form => ({ name: r.name, code: r.code, languageCode: r.languageCode ?? '', isActive: r.isActive });

export function TenantEditorPage() {
  const { t } = useI18n();
  const toast = useToast();
  const navigate = useNavigate();
  const { can } = useAuth();
  const id = Number(useParams().id);
  const canWrite = can(P.TenantManage);
  const [tenant, setTenant] = useState<TenantRead | null>(null);
  const [form, setForm] = useState<Form | null>(null);
  const [companies, setCompanies] = useState<CompanyRead[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const languages = useOptions(() => languagesApi.list(), r => ({ value: r.id, label: `${r.name} (${r.code})` }));

  const load = async () => {
    try {
      const [tn, all] = await Promise.all([tenantsApi.get(id), companiesApi.list()]);
      setTenant(tn); setForm(toForm(tn)); setCompanies(all.filter(c => c.tenantId === id));
    } catch (e) { toast.error((e as Error).message); setTenant(null); } finally { setLoading(false); }
  };
  useEffect(() => { void load(); }, [id]); // eslint-disable-line react-hooks/exhaustive-deps

  if (loading) return <Spinner label={t('Loading…')} />;
  if (!tenant || !form) return <EmptyState title={t('Tenant not found')} />;
  const dirty = JSON.stringify(form) !== JSON.stringify(toForm(tenant));
  const set = <K extends keyof Form>(k: K, v: Form[K]) => setForm(f => (f ? { ...f, [k]: v } : f));

  const save = async () => {
    setSaving(true);
    setErrors({});
    try {
      const updated = await tenantsApi.update(tenant.id, { name: form.name.trim(), code: form.code.trim().toUpperCase(), languageCode: form.languageCode || null, isActive: form.isActive });
      setTenant(updated); setForm(toForm(updated));
      toast.success(t('Tenant saved'));
    } catch (e) {
      if (e instanceof ApiException) {
        const next: Record<string, string> = {};
        for (const f of ['name', 'code', 'languageCode']) { const m = e.fieldMessage(f); if (m) next[f] = m; }
        setErrors(next);
        if (Object.keys(next).length === 0) toast.error(e.message);
      } else toast.error((e as Error).message);
    } finally { setSaving(false); }
  };

  const languageCodes = languages.rows.map(l => ({ value: l.code, label: `${l.name} (${l.code})` }));

  return (
    <>
      <PageHeader title={tenant.name} actions={<Button outlined severity="secondary" icon="pi pi-arrow-left" label={t('Back')} onClick={() => navigate('/admin/tenants')} />} />
      <div className="card">
        <TabView>
          <TabPanel header={t('General')} leftIcon="pi pi-info-circle mr-2">
            <div className="form-grid" style={{ maxWidth: 760 }}>
              <TextField label={t('Tenant name')} required value={form.name} error={errors.name} disabled={!canWrite} onChange={e => set('name', e.target.value)} />
              <TextField label={t('Tenant code')} required value={form.code} error={errors.code} disabled={!canWrite} onChange={e => set('code', e.target.value.toUpperCase())} />
              <SelectField label={t('Default language')} placeholder={t('Inherit from the platform')} value={form.languageCode} options={languageCodes} error={errors.languageCode} disabled={!canWrite}
                           onChange={e => set('languageCode', e.target.value)} />
              <div />
              <CheckField label={t('Active')} checked={form.isActive} disabled={!canWrite} onChange={v => set('isActive', v)} />
            </div>
          </TabPanel>
          <TabPanel header={`${t('Companies')} (${companies.length})`} leftIcon="pi pi-building mr-2">
            {companies.length === 0 ? <EmptyState title={t('No companies yet')} text={t('Add one from the Companies screen.')} /> : (
              <div className="stack" style={{ maxWidth: 760 }}>
                {companies.map(c => (
                  <div key={c.id} className="card card-pad row">
                    <i className="pi pi-building" /><div className="grow"><div className="cell-main">{c.name}</div><div className="cell-sub">{c.code}</div></div>
                    <Button text size="small" label={t('Edit')} onClick={() => navigate(`/admin/companies/${c.id}`)} />
                  </div>
                ))}
              </div>
            )}
          </TabPanel>
        </TabView>
      </div>
      {canWrite && <SaveBar dirty={dirty} saving={saving} canSave={!!form.name.trim() && !!form.code.trim()} onSave={() => void save()} onDiscard={() => { setForm(toForm(tenant)); setErrors({}); }} />}
    </>
  );
}
