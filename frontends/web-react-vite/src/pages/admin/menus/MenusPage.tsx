import { useCallback, useEffect, useMemo, useState } from 'react';
import { Button } from 'primereact/button';
import { Column } from 'primereact/column';
import { TreeTable } from 'primereact/treetable';
import { Badge, CheckField, PageHeader, SelectField, TextField } from '@/components/ui/Form';
import { ConfirmDialog, Modal } from '@/components/ui/Modal';
import { useToast } from '@/components/ui/Toast';
import { useAuth } from '@/features/auth/AuthContext';
import { P } from '@/features/auth/permissions';
import { definitionsApi, type ApplicationTypeRead, type MenuFeatureToggleRead } from '@/features/definitions/definitions.service';
import { buildMenuTree, menusApi, type MenuRead, type MenuTypeRead, type MenuWrite } from '@/features/menus/menus.service';
import { rolesApi, type PermissionRead } from '@/features/roles/roles.service';
import { useI18n } from '@/i18n';

const toWrite = (m: MenuRead): MenuWrite => ({
  parentId: m.parentId, code: m.code, title: m.title, path: m.path, icon: m.icon, displayOrder: m.displayOrder, menuTypeId: m.menuTypeId,
  applicationTypeId: m.applicationTypeId, permissionId: m.permissionId, moduleCode: m.moduleCode, featureCode: m.featureCode, isActive: m.isActive,
});
const blank = (typeId: number | null): MenuWrite => ({ code: '', title: '', path: '', icon: '', displayOrder: 0, menuTypeId: typeId, applicationTypeId: null, parentId: null, permissionId: null, featureCode: null, isActive: true });

export function MenusPage() {
  const { t } = useI18n();
  const toast = useToast();
  const { can } = useAuth();
  const canWrite = can(P.MenuDesign);

  const [menus, setMenus] = useState<MenuRead[]>([]);
  const [types, setTypes] = useState<MenuTypeRead[]>([]);
  const [toggles, setToggles] = useState<MenuFeatureToggleRead[]>([]);
  const [apps, setApps] = useState<ApplicationTypeRead[]>([]);
  const [perms, setPerms] = useState<PermissionRead[]>([]);
  const [loading, setLoading] = useState(true);
  const [edit, setEdit] = useState<{ id: number | null; form: MenuWrite } | null>(null);
  const [deleting, setDeleting] = useState<MenuRead | null>(null);
  const [saving, setSaving] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [m, ty, tg, ap, pe] = await Promise.all([
        menusApi.list(), menusApi.types.list(), definitionsApi.menuToggles.list().catch(() => []), definitionsApi.applicationTypes.list().catch(() => []), rolesApi.permissions.list().catch(() => []),
      ]);
      setMenus(m); setTypes(ty); setToggles(tg); setApps(ap); setPerms(pe);
    } catch (e) { toast.error((e as Error).message); } finally { setLoading(false); }
  }, []); // eslint-disable-line react-hooks/exhaustive-deps
  useEffect(() => { void load(); }, [load]);

  const tree = useMemo(() => buildMenuTree(menus), [menus]);
  const typeName = (id?: number | null) => types.find(x => x.id === id)?.name ?? (id ? `#${id}` : '—');
  const appOf = (id?: number | null) => apps.find(a => a.id === id);

  const save = async () => {
    if (!edit) return;
    setSaving(true);
    try {
      if (edit.id) await menusApi.update(edit.id, edit.form); else await menusApi.create(edit.form);
      toast.success(t('Menu saved')); setEdit(null); await load();
    } catch (e) { toast.error((e as Error).message); } finally { setSaving(false); }
  };

  const remove = async () => {
    if (!deleting) return;
    setSaving(true);
    try { await menusApi.remove(deleting.id); toast.success(t('Menu deleted')); setDeleting(null); await load(); }
    catch (e) { toast.error((e as Error).message); } finally { setSaving(false); }
  };

  const form = edit?.form;
  const set = (patch: Partial<MenuWrite>) => setEdit(e => (e ? { ...e, form: { ...e.form, ...patch } } : e));
  const num = (v: string) => (v ? Number(v) : null);

  return (
    <>
      <PageHeader title={t('Menu management')} subtitle={t('{n} records in total', { n: menus.length })}
                  actions={<>
                    <Button text severity="secondary" icon="pi pi-refresh" aria-label={t('Refresh')} onClick={() => void load()} />
                    {canWrite && <Button icon="pi pi-plus" label={t('New menu')} onClick={() => setEdit({ id: null, form: blank(types[0]?.id ?? null) })} />}
                  </>} />
      <div className="card">
        <TreeTable value={tree} loading={loading} tableStyle={{ minWidth: 760 }} className="p-treetable-sm">
          <Column expander header={t('Name')} body={n => <span><b>{n.data.title}</b> <span className="muted-text">· {n.data.code}</span></span>} />
          <Column header={t('Route')} body={n => (n.data.path ? <code>{n.data.path}</code> : <span className="muted-text">—</span>)} />
          <Column header={t('Icon')} style={{ width: 70 }} body={n => (n.data.icon ? <i className={`pi pi-${n.data.icon}`} /> : null)} />
          <Column header={t('Type')} style={{ width: 120 }} body={n => typeName(n.data.menuTypeId)} />
          <Column header={t('Scope')} style={{ width: 130 }} body={n => (appOf(n.data.applicationTypeId) ? <Badge tone="info">{appOf(n.data.applicationTypeId)!.name}</Badge> : <span className="muted-text">{t('Shared')}</span>)} />
          <Column header={t('Visible')} style={{ width: 90 }} body={n => (n.data.isActive ? <Badge tone="success">{t('Yes')}</Badge> : <Badge>{t('No')}</Badge>)} />
          <Column header="" style={{ width: 110 }} body={n => (canWrite ? (
            <div className="row">
              <Button text rounded severity="secondary" icon="pi pi-pencil" aria-label={t('Edit')} onClick={() => setEdit({ id: n.data.id, form: toWrite(n.data) })} />
              <Button text rounded severity="danger" icon="pi pi-trash" aria-label={t('Delete')} onClick={() => setDeleting(n.data)} />
            </div>
          ) : null)} />
        </TreeTable>
      </div>

      {edit && form && (
        <Modal title={edit.id ? t('Edit menu') : t('New menu')} onClose={() => setEdit(null)} width={560}
               footer={<><Button text severity="secondary" label={t('Cancel')} onClick={() => setEdit(null)} disabled={saving} /><Button icon="pi pi-check" label={t('Save')} loading={saving} disabled={!form.title.trim() || !form.code.trim()} onClick={() => void save()} /></>}>
          <div className="form-grid">
            <TextField label={t('Code')} required value={form.code} disabled={!!edit.id} onChange={e => set({ code: e.target.value.toUpperCase() })} />
            <SelectField label={t('Parent menu')} placeholder={t('Root menu')} value={form.parentId} options={menus.filter(m => m.id !== edit.id).map(m => ({ value: m.id, label: m.title }))} onChange={e => set({ parentId: num(e.target.value) })} />
            <TextField label={t('Name')} required value={form.title} onChange={e => set({ title: e.target.value })} />
            <TextField label={t('Route')} value={form.path ?? ''} onChange={e => set({ path: e.target.value || null })} />
            <TextField label={t('Icon')} value={form.icon ?? ''} hint={t('A PrimeIcons name, e.g. users')} onChange={e => set({ icon: e.target.value || null })} />
            <TextField label={t('Order')} type="number" value={String(form.displayOrder)} onChange={e => set({ displayOrder: Number(e.target.value) || 0 })} />
            <SelectField label={t('Type')} placeholder="—" value={form.menuTypeId} options={types.map(x => ({ value: x.id, label: x.name }))} onChange={e => set({ menuTypeId: num(e.target.value) })} />
            <SelectField label={t('Application (scope)')} placeholder={t('Shared (all applications)')} value={form.applicationTypeId} options={apps.map(a => ({ value: a.id, label: a.name }))} onChange={e => set({ applicationTypeId: num(e.target.value) })} />
            <SelectField label={t('Required permission')} placeholder={t('None')} value={form.permissionId} options={perms.map(p => ({ value: p.id, label: p.code }))} onChange={e => set({ permissionId: num(e.target.value) })} />
            <SelectField label={t('Feature switch')} placeholder={t('None')} value={form.featureCode} options={toggles.map(x => ({ value: x.featureCode, label: x.featureCode }))} onChange={e => set({ featureCode: e.target.value || null })} />
            <CheckField label={t('Visible')} checked={form.isActive ?? true} onChange={v => set({ isActive: v })} />
          </div>
        </Modal>
      )}

      {deleting && <ConfirmDialog title={t('Delete menu')} message={t('Delete {name}? It can be restored later.', { name: deleting.title })} confirmLabel={t('Delete')} danger busy={saving} onCancel={() => setDeleting(null)} onConfirm={() => void remove()} />}
    </>
  );
}
