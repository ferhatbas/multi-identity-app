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
import {
  companiesApi, companyTypesApi, countriesApi, languagesApi, tenantsApi, type CompanyRead, type CompanyWrite,
} from '@/features/tenants/organization.service';
import { ApiException } from '@/services/http/httpService';
import { useI18n } from '@/i18n';

const FIELDS = ['name', 'code', 'tenantId', 'companyTypeId', 'languageCode', 'email', 'phoneNumber', 'website', 'address', 'address2', 'city', 'state', 'zip', 'countryId'];

function useLookups() {
  const { user } = useAuth();
  const types = useOptions(() => companyTypesApi.list(), r => ({ value: r.id, label: r.name }));
  const tenants = useOptions(() => (user?.tenantId ? Promise.resolve([]) : tenantsApi.list()), r => ({ value: r.id, label: r.name }));
  const countries = useOptions(() => countriesApi.list(), r => ({ value: r.id, label: r.name }));
  const languages = useOptions(() => languagesApi.list(), r => ({ value: r.id, label: `${r.name} (${r.code})` }));
  return { types, tenants, countries, languages, tenantScoped: !!user?.tenantId };
}

const fieldErrors = (e: unknown): Record<string, string> | null => {
  if (!(e instanceof ApiException)) return null;
  const next: Record<string, string> = {};
  for (const f of FIELDS) { const m = e.fieldMessage(f); if (m) next[f] = m; }
  return Object.keys(next).length ? next : null;
};

export function CompaniesListPage() {
  const { t, formatDate } = useI18n();
  const toast = useToast();
  const navigate = useNavigate();
  const { can } = useAuth();
  const { types } = useLookups();
  const canWrite = can(P.CompanyManage);
  const table = usePagedTable<CompanyRead>(companiesApi.paged, { orderBy: 'name', pageSize: 12 });
  const [deleting, setDeleting] = useState<CompanyRead | null>(null);
  const [busy, setBusy] = useState(false);

  const remove = async () => {
    if (!deleting) return;
    setBusy(true);
    try { await companiesApi.remove(deleting.id); toast.success(t('Company deleted')); setDeleting(null); table.reload(); }
    catch (e) { toast.error((e as Error).message); } finally { setBusy(false); }
  };

  const columns = useMemo<Column<CompanyRead>[]>(() => [
    { key: 'name', header: t('Company'), sortKey: 'name', csv: r => r.name,
      render: r => <div className="row"><Avatar id={r.id} text={r.name} /><div><div className="cell-main">{r.name}</div><div className="cell-sub">{r.email ?? ''}</div></div></div> },
    { key: 'code', header: t('Code'), sortKey: 'code', csv: r => r.code, render: r => <code>{r.code}</code> },
    { key: 'type', header: t('Type'), csv: r => types.options.find(o => o.value === r.companyTypeId)?.label, render: r => types.options.find(o => o.value === r.companyTypeId)?.label ?? '—' },
    { key: 'city', header: t('City'), sortKey: 'city', csv: r => r.city, render: r => r.city ?? '—' },
    { key: 'status', header: t('Status'), csv: r => r.isActive, render: r => (r.isActive ? <Badge tone="success">{t('Active')}</Badge> : <Badge>{t('Inactive')}</Badge>) },
    { key: 'created', header: t('Created'), sortKey: 'createdAt', csv: r => r.createdAt, render: r => formatDate(r.createdAt, false) },
  ], [t, formatDate, types.options]);

  const menu = (r: CompanyRead): MenuItem[] => [
    { label: t('Edit'), icon: 'pi pi-pencil', command: () => navigate(`/admin/companies/${r.id}`) },
    ...(canWrite ? [{ separator: true }, { label: t('Delete'), icon: 'pi pi-trash', className: 'menu-danger', command: () => setDeleting(r) }] : []),
  ];

  return (
    <>
      <PageHeader title={t('Companies')} subtitle={t('The companies of your tenant.')}
                  actions={canWrite && <Button icon="pi pi-plus" label={t('New company')} onClick={() => navigate('/admin/companies/new')} />} />
      <div className="toolbar">
        <SearchBox onChange={table.setSearch} placeholder={t('Search by name or code…')} />
        <div className="grow" />
        <Button size="small" outlined severity="secondary" icon="pi pi-download" label={t('Export CSV')} disabled={table.rows.length === 0} onClick={() => exportCsv(columns, table.rows, 'companies.csv')} />
        <Button text severity="secondary" icon="pi pi-refresh" aria-label={t('Refresh')} onClick={table.reload} />
      </div>
      <DataTable columns={columns} table={table} rowKey={r => r.id} rowClass={r => (r.isActive ? undefined : 'row-muted')} onRowClick={r => navigate(`/admin/companies/${r.id}`)}
                 emptyTitle={t('No companies yet')}
                 actions={r => <><Button text rounded severity="secondary" icon="pi pi-pencil" aria-label={t('Edit')} onClick={() => navigate(`/admin/companies/${r.id}`)} /><RowMenu items={menu(r)} /></>} />
      {deleting && <ConfirmDialog title={t('Delete company')} message={t('Delete {name}? It can be restored later.', { name: deleting.name })} confirmLabel={t('Delete')} danger busy={busy} onCancel={() => setDeleting(null)} onConfirm={() => void remove()} />}
    </>
  );
}

interface Form {
  name: string; code: string; tenantId: string; companyTypeId: string; languageCode: string;
  email: string; phoneNumber: string; website: string; address: string; address2: string; city: string; state: string; zip: string; countryId: string;
  isActive: boolean;
}

const blank: Form = { name: '', code: '', tenantId: '', companyTypeId: '', languageCode: '', email: '', phoneNumber: '', website: '', address: '', address2: '', city: '', state: '', zip: '', countryId: '', isActive: true };
const toForm = (r: CompanyRead): Form => ({
  name: r.name, code: r.code, tenantId: r.tenantId?.toString() ?? '', companyTypeId: r.companyTypeId?.toString() ?? '', languageCode: r.languageCode ?? '',
  email: r.email ?? '', phoneNumber: r.phoneNumber ?? '', website: r.website ?? '', address: r.address ?? '', address2: r.address2 ?? '', city: r.city ?? '', state: r.state ?? '', zip: r.zip ?? '',
  countryId: r.countryId?.toString() ?? '', isActive: r.isActive,
});
const text = (v: string) => v.trim() || null;
const num = (v: string) => (v ? Number(v) : null);
const toWrite = (f: Form): CompanyWrite => ({
  name: f.name.trim(), code: f.code.trim().toUpperCase(), tenantId: num(f.tenantId), companyTypeId: num(f.companyTypeId), languageCode: text(f.languageCode),
  email: text(f.email), phoneNumber: text(f.phoneNumber), website: text(f.website), address: text(f.address), address2: text(f.address2), city: text(f.city), state: text(f.state), zip: text(f.zip),
  countryId: num(f.countryId), isActive: f.isActive,
});

interface Setter { form: Form; set: <K extends keyof Form>(k: K, v: Form[K]) => void; errors: Record<string, string>; disabled?: boolean }

function GeneralFields({ form, set, errors, disabled, creating }: Setter & { creating?: boolean }) {
  const { t } = useI18n();
  const { types, tenants, languages, tenantScoped } = useLookups();
  const languageCodes = languages.rows.map(l => ({ value: l.code, label: `${l.name} (${l.code})` }));
  return (
    <div className="form-grid" style={{ maxWidth: 760 }}>
      <TextField label={t('Company name')} required value={form.name} error={errors.name} disabled={disabled} onChange={e => set('name', e.target.value)} />
      <TextField label={t('Company code')} required value={form.code} error={errors.code} disabled={disabled} maxLength={32} onChange={e => set('code', e.target.value.toUpperCase())} />
      {!tenantScoped && <SelectField label={t('Tenant')} required placeholder="—" value={form.tenantId} options={tenants.options} error={errors.tenantId} disabled={disabled || !creating} onChange={e => set('tenantId', e.target.value)} />}
      <SelectField label={t('Company type')} placeholder="—" value={form.companyTypeId} options={types.options} error={errors.companyTypeId} disabled={disabled} onChange={e => set('companyTypeId', e.target.value)} />
      <SelectField label={t('Language')} placeholder={t('Inherit from the tenant')} value={form.languageCode} options={languageCodes} error={errors.languageCode} disabled={disabled} onChange={e => set('languageCode', e.target.value)} />
      <div />
      <CheckField label={t('Active')} checked={form.isActive} disabled={disabled} onChange={v => set('isActive', v)} />
    </div>
  );
}

function ContactFields({ form, set, errors, disabled }: Setter) {
  const { t } = useI18n();
  const { countries } = useLookups();
  return (
    <div className="form-grid" style={{ maxWidth: 760 }}>
      <TextField label={t('E-mail')} type="email" value={form.email} error={errors.email} disabled={disabled} maxLength={256} onChange={e => set('email', e.target.value)} />
      <TextField label={t('Phone')} value={form.phoneNumber} error={errors.phoneNumber} disabled={disabled} maxLength={64} onChange={e => set('phoneNumber', e.target.value)} />
      <TextField label={t('Website')} value={form.website} error={errors.website} disabled={disabled} maxLength={256} onChange={e => set('website', e.target.value)} />
      <div />
      <TextField wide label={t('Address')} value={form.address} error={errors.address} disabled={disabled} maxLength={512} onChange={e => set('address', e.target.value)} />
      <TextField wide label={t('Address line 2')} value={form.address2} error={errors.address2} disabled={disabled} maxLength={512} onChange={e => set('address2', e.target.value)} />
      <TextField label={t('City')} value={form.city} error={errors.city} disabled={disabled} maxLength={128} onChange={e => set('city', e.target.value)} />
      <TextField label={t('State / region')} value={form.state} error={errors.state} disabled={disabled} maxLength={128} onChange={e => set('state', e.target.value)} />
      <TextField label={t('Postal code')} value={form.zip} error={errors.zip} disabled={disabled} maxLength={32} onChange={e => set('zip', e.target.value)} />
      <SelectField label={t('Country')} placeholder="—" value={form.countryId} options={countries.options} error={errors.countryId} disabled={disabled} onChange={e => set('countryId', e.target.value)} />
    </div>
  );
}

export function CompanyCreatePage() {
  const { t } = useI18n();
  const toast = useToast();
  const navigate = useNavigate();
  const { user } = useAuth();
  const [form, setForm] = useState<Form>({ ...blank, tenantId: user?.tenantId?.toString() ?? '' });
  const [busy, setBusy] = useState(false);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const set = <K extends keyof Form>(k: K, v: Form[K]) => setForm(f => ({ ...f, [k]: v }));

  const create = async () => {
    setBusy(true);
    setErrors({});
    try {
      const company = await companiesApi.create(toWrite(form));
      toast.success(t('Company created'));
      navigate(`/admin/companies/${company.id}`, { replace: true });
    } catch (e) {
      const next = fieldErrors(e);
      if (next) setErrors(next); else toast.error((e as Error).message);
    } finally { setBusy(false); }
  };

  const ready = !!form.name.trim() && !!form.code.trim() && (!!user?.tenantId || !!form.tenantId);

  return (
    <>
      <PageHeader title={t('New company')} />
      <div className="card">
        <TabView>
          <TabPanel header={t('General')} leftIcon="pi pi-info-circle mr-2"><GeneralFields form={form} set={set} errors={errors} creating /></TabPanel>
          <TabPanel header={t('Contact')} leftIcon="pi pi-map-marker mr-2"><ContactFields form={form} set={set} errors={errors} /></TabPanel>
        </TabView>
      </div>
      <div className="save-bar">
        <span className="muted-text" />
        <Button text severity="secondary" label={t('Cancel')} onClick={() => navigate('/admin/companies')} disabled={busy} />
        <Button icon="pi pi-check" label={t('Create')} disabled={!ready || busy} loading={busy} onClick={() => void create()} />
      </div>
    </>
  );
}

export function CompanyEditorPage() {
  const { t } = useI18n();
  const toast = useToast();
  const navigate = useNavigate();
  const { can } = useAuth();
  const id = Number(useParams().id);
  const canWrite = can(P.CompanyManage);
  const [company, setCompany] = useState<CompanyRead | null>(null);
  const [form, setForm] = useState<Form | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [errors, setErrors] = useState<Record<string, string>>({});

  useEffect(() => {
    companiesApi.get(id).then(c => { setCompany(c); setForm(toForm(c)); })
      .catch(e => { toast.error((e as Error).message); setCompany(null); }).finally(() => setLoading(false));
  }, [id]); // eslint-disable-line react-hooks/exhaustive-deps

  if (loading) return <Spinner label={t('Loading…')} />;
  if (!company || !form) return <EmptyState title={t('Company not found')} />;
  const dirty = JSON.stringify(form) !== JSON.stringify(toForm(company));
  const set = <K extends keyof Form>(k: K, v: Form[K]) => setForm(f => (f ? { ...f, [k]: v } : f));

  const save = async () => {
    setSaving(true);
    setErrors({});
    try {
      const updated = await companiesApi.update(company.id, toWrite(form));
      setCompany(updated); setForm(toForm(updated));
      toast.success(t('Company saved'));
    } catch (e) {
      const next = fieldErrors(e);
      if (next) setErrors(next); else toast.error((e as Error).message);
    } finally { setSaving(false); }
  };

  return (
    <>
      <PageHeader title={company.name} actions={<Button outlined severity="secondary" icon="pi pi-arrow-left" label={t('Back')} onClick={() => navigate('/admin/companies')} />} />
      <div className="card">
        <TabView>
          <TabPanel header={t('General')} leftIcon="pi pi-info-circle mr-2"><GeneralFields form={form} set={set} errors={errors} disabled={!canWrite} /></TabPanel>
          <TabPanel header={t('Contact')} leftIcon="pi pi-map-marker mr-2"><ContactFields form={form} set={set} errors={errors} disabled={!canWrite} /></TabPanel>
        </TabView>
      </div>
      {canWrite && <SaveBar dirty={dirty} saving={saving} canSave={!!form.name.trim() && !!form.code.trim()} onSave={() => void save()} onDiscard={() => { setForm(toForm(company)); setErrors({}); }} />}
    </>
  );
}
