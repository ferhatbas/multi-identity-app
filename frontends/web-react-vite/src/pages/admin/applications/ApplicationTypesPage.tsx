import { useCallback, useEffect, useMemo, useState } from 'react';
import { Button } from 'primereact/button';
import { Checkbox } from 'primereact/checkbox';
import { Column } from 'primereact/column';
import { DataTable as PrimeTable } from 'primereact/datatable';
import { Badge, CheckField, PageHeader, TextField } from '@/components/ui/Form';
import { ConfirmDialog, Modal } from '@/components/ui/Modal';
import { useToast } from '@/components/ui/Toast';
import { useAuth } from '@/features/auth/AuthContext';
import { P } from '@/features/auth/permissions';
import { definitionsApi, type ApplicationTypeRead, type CompanyTypeApplicationRead, type CompanyTypeRead } from '@/features/definitions/definitions.service';
import { useI18n } from '@/i18n';

interface Form { name: string; code: string; description: string; icon: string; displayOrder: number; isActive: boolean }
const blank = (order: number): Form => ({ name: '', code: '', description: '', icon: '', displayOrder: order, isActive: true });
const toForm = (a: ApplicationTypeRead): Form => ({ name: a.name, code: a.code, description: a.description ?? '', icon: a.icon ?? '', displayOrder: a.displayOrder, isActive: a.isActive });
const toBody = (f: Form) => ({ name: f.name.trim(), code: f.code.trim().toUpperCase(), description: f.description.trim() || null, icon: f.icon.trim() || null, displayOrder: f.displayOrder, isActive: f.isActive });

export function ApplicationTypesPage() {
  const { t } = useI18n();
  const toast = useToast();
  const { can } = useAuth();
  const canWrite = can(P.ApplicationTypeManage);

  const [apps, setApps] = useState<ApplicationTypeRead[]>([]);
  const [companyTypes, setCompanyTypes] = useState<CompanyTypeRead[]>([]);
  const [grants, setGrants] = useState<CompanyTypeApplicationRead[]>([]);
  const [loading, setLoading] = useState(true);
  const [edit, setEdit] = useState<{ id: number | null; form: Form } | null>(null);
  const [deleting, setDeleting] = useState<ApplicationTypeRead | null>(null);
  const [saving, setSaving] = useState(false);
  const [busyCell, setBusyCell] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [a, ct, g] = await Promise.all([definitionsApi.applicationTypes.list(), definitionsApi.companyTypes.list(), definitionsApi.companyTypeApplications.list()]);
      setApps([...a].sort((x, y) => x.displayOrder - y.displayOrder)); setCompanyTypes(ct); setGrants(g);
    } catch (e) { toast.error((e as Error).message); } finally { setLoading(false); }
  }, []); // eslint-disable-line react-hooks/exhaustive-deps
  useEffect(() => { void load(); }, [load]);

  const grantOf = useMemo(() => new Map(grants.map(g => [`${g.companyTypeId}:${g.applicationTypeId}`, g])), [grants]);

  const save = async () => {
    if (!edit) return;
    setSaving(true);
    try {
      if (edit.id) await definitionsApi.applicationTypes.update(edit.id, toBody(edit.form)); else await definitionsApi.applicationTypes.create(toBody(edit.form));
      setEdit(null); toast.success(t('Saved')); await load();
    } catch (e) { toast.error((e as Error).message); } finally { setSaving(false); }
  };

  const remove = async () => {
    if (!deleting) return;
    setSaving(true);
    try { await definitionsApi.applicationTypes.remove(deleting.id); setDeleting(null); await load(); }
    catch (e) { toast.error((e as Error).message); } finally { setSaving(false); }
  };

  const toggle = async (ct: CompanyTypeRead, app: ApplicationTypeRead, on: boolean) => {
    const key = `${ct.id}:${app.id}`;
    setBusyCell(key);
    try {
      const existing = grantOf.get(key);
      if (on && !existing) await definitionsApi.companyTypeApplications.create({ companyTypeId: ct.id, applicationTypeId: app.id, isDefault: false });
      else if (!on && existing) await definitionsApi.companyTypeApplications.remove(existing.id);
      await load();
    } catch (e) { toast.error((e as Error).message); } finally { setBusyCell(null); }
  };

  const form = edit?.form;
  const set = (patch: Partial<Form>) => setEdit(e => (e ? { ...e, form: { ...e.form, ...patch } } : e));

  return (
    <>
      <PageHeader title={t('Application types')} subtitle={t('{n} records in total', { n: apps.length })}
                  actions={<>
                    <Button text severity="secondary" icon="pi pi-refresh" aria-label={t('Refresh')} onClick={() => void load()} />
                    {canWrite && <Button icon="pi pi-plus" label={t('New application')} onClick={() => setEdit({ id: null, form: blank(apps.length + 1) })} />}
                  </>} />

      <div className="card" style={{ marginBottom: 16 }}>
        <PrimeTable value={apps} loading={loading} size="small" tableStyle={{ minWidth: 640 }} emptyMessage={t('No applications yet')}>
          <Column header={t('Icon')} style={{ width: 60 }} body={(a: ApplicationTypeRead) => (a.icon ? <i className={a.icon.startsWith('pi') ? a.icon : `pi pi-${a.icon}`} /> : null)} />
          <Column header={t('Name')} body={(a: ApplicationTypeRead) => <b>{a.name}</b>} />
          <Column header={t('Code')} body={(a: ApplicationTypeRead) => <code>{a.code}</code>} />
          <Column header={t('Description')} body={(a: ApplicationTypeRead) => a.description || '—'} />
          <Column header="#" style={{ width: 60 }} body={(a: ApplicationTypeRead) => a.displayOrder} />
          <Column header={t('Status')} style={{ width: 100 }} body={(a: ApplicationTypeRead) => (a.isActive ? <Badge tone="success">{t('Active')}</Badge> : <Badge>{t('Inactive')}</Badge>)} />
          <Column header="" style={{ width: 110 }} body={(a: ApplicationTypeRead) => (canWrite ? (
            <div className="row">
              <Button text rounded severity="secondary" icon="pi pi-pencil" aria-label={t('Edit')} onClick={() => setEdit({ id: a.id, form: toForm(a) })} />
              <Button text rounded severity="danger" icon="pi pi-trash" aria-label={t('Delete')} onClick={() => setDeleting(a)} />
            </div>
          ) : null)} />
        </PrimeTable>
      </div>

      <div className="card">
        <div style={{ padding: '14px 16px 0' }}>
          <h3>{t('Company type → application grants')}</h3>
          <p className="muted-text">{t('Which company type may use which application. A role of one application can only be given in companies whose type allows it, and the application switcher lists these.')}</p>
        </div>
        <div style={{ overflowX: 'auto' }}>
          <table className="access-matrix" style={{ minWidth: 560 }}>
            <thead>
              <tr>
                <th className="sticky-col">{t('Company type')}</th>
                {apps.map(a => <th key={a.id}>{a.name}</th>)}
              </tr>
            </thead>
            <tbody>
              {companyTypes.map(ct => (
                <tr key={ct.id}>
                  <th className="sticky-col"><b>{ct.name}</b> <span className="muted-text">· {ct.code}</span></th>
                  {apps.map(a => {
                    const key = `${ct.id}:${a.id}`;
                    return (
                      <td key={a.id}>
                        {busyCell === key ? <i className="pi pi-spin pi-spinner" style={{ fontSize: 14 }} />
                          : <Checkbox checked={grantOf.has(key)} disabled={!canWrite} onChange={e => void toggle(ct, a, !!e.checked)} />}
                      </td>
                    );
                  })}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      {edit && form && (
        <Modal title={edit.id ? t('Edit application') : t('New application')} onClose={() => setEdit(null)} width={520}
               footer={<><Button text severity="secondary" label={t('Cancel')} onClick={() => setEdit(null)} disabled={saving} /><Button label={t('Save')} loading={saving} disabled={!form.name.trim() || !form.code.trim()} onClick={() => void save()} /></>}>
          <div className="form-grid">
            <TextField label={t('Name')} required value={form.name} maxLength={256} onChange={e => set({ name: e.target.value })} />
            <TextField label={t('Code')} required value={form.code} maxLength={32} onChange={e => set({ code: e.target.value.toUpperCase() })} />
            <TextField wide label={t('Description')} value={form.description} maxLength={512} onChange={e => set({ description: e.target.value })} />
            <TextField label={t('Icon')} value={form.icon} placeholder="pi pi-heart" onChange={e => set({ icon: e.target.value })} />
            <TextField label={t('Display order')} type="number" value={String(form.displayOrder)} onChange={e => set({ displayOrder: Number(e.target.value) || 0 })} />
            <CheckField label={t('Active')} checked={form.isActive} onChange={v => set({ isActive: v })} />
          </div>
        </Modal>
      )}

      {deleting && <ConfirmDialog title={t('Delete application')} message={t('Delete {name}? It can be restored later.', { name: deleting.name })} confirmLabel={t('Delete')} danger busy={saving} onCancel={() => setDeleting(null)} onConfirm={() => void remove()} />}
    </>
  );
}
