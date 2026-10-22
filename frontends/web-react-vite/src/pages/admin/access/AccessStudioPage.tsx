import { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Button } from 'primereact/button';
import { Column } from 'primereact/column';
import { DataTable as PrimeTable } from 'primereact/datatable';
import { TabPanel, TabView } from 'primereact/tabview';
import { TreeTable } from 'primereact/treetable';
import { Badge, CheckField, PageHeader, SelectField, TextField } from '@/components/ui/Form';
import { ConfirmDialog, Modal } from '@/components/ui/Modal';
import { useToast } from '@/components/ui/Toast';
import { useAuth } from '@/features/auth/AuthContext';
import { P } from '@/features/auth/permissions';
import { buildMenuTree, menusApi, type MenuRead, type MenuTypeRead, type MenuWrite } from '@/features/menus/menus.service';
import { rolesApi, type ApplicationOption, type PermissionRead, type RolePermissionRead, type RoleRead, type RoleWrite } from '@/features/roles/roles.service';
import { useI18n } from '@/i18n';
import { PermissionCatalogue, PermissionMatrix } from './AccessParts';

type Scope = 'shared' | number;

const roleWrite = (r: RoleRead, patch: Partial<RoleWrite> = {}): RoleWrite => ({ name: r.name, code: r.code, description: r.description, applicationTypeId: r.applicationTypeId, isActive: r.isActive, ...patch });
const menuWrite = (m: MenuRead, patch: Partial<MenuWrite> = {}): MenuWrite => ({
  parentId: m.parentId, code: m.code, title: m.title, path: m.path, icon: m.icon, displayOrder: m.displayOrder, menuTypeId: m.menuTypeId,
  applicationTypeId: m.applicationTypeId, permissionId: m.permissionId, moduleCode: m.moduleCode, featureCode: m.featureCode, isActive: m.isActive, ...patch,
});

export function AccessStudioPage() {
  const { t } = useI18n();
  const toast = useToast();
  const navigate = useNavigate();
  const { can } = useAuth();
  const canRoles = can(P.RoleManage);
  const canMenus = can(P.MenuDesign);

  const [apps, setApps] = useState<ApplicationOption[]>([]);
  const [scope, setScope] = useState<Scope>('shared');
  const [menus, setMenus] = useState<MenuRead[]>([]);
  const [menuTypes, setMenuTypes] = useState<MenuTypeRead[]>([]);
  const [roles, setRoles] = useState<RoleRead[]>([]);
  const [perms, setPerms] = useState<PermissionRead[]>([]);
  const [grants, setGrants] = useState<RolePermissionRead[]>([]);
  const [loading, setLoading] = useState(true);
  const [menuEdit, setMenuEdit] = useState<{ id: number | null; form: MenuWrite } | null>(null);
  const [roleEdit, setRoleEdit] = useState<{ id: number | null; form: RoleWrite } | null>(null);
  const [deleting, setDeleting] = useState<{ kind: 'menu' | 'role'; id: number; name: string } | null>(null);
  const [saving, setSaving] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [a, r, p, g, m, mt] = await Promise.all([
        rolesApi.applications.list(), rolesApi.list(), rolesApi.permissions.list(), rolesApi.grants.list().catch(() => []),
        canMenus ? menusApi.list().catch(() => []) : Promise.resolve([]), canMenus ? menusApi.types.list().catch(() => []) : Promise.resolve([]),
      ]);
      setApps(a); setRoles(r); setPerms(p); setGrants(g); setMenus(m); setMenuTypes(mt);
    } catch (e) { toast.error((e as Error).message); } finally { setLoading(false); }
  }, [canMenus]); // eslint-disable-line react-hooks/exhaustive-deps
  useEffect(() => { void load(); }, [load]);

  const scopeAppId = scope === 'shared' ? null : scope;
  const inScope = (appId: number | null | undefined) => (scopeAppId === null ? appId == null : appId === scopeAppId);
  const scopeLabel = scope === 'shared' ? t('Shared') : apps.find(a => a.id === scope)?.name ?? '';
  const scopeOptions = [{ value: 'shared', label: t('Shared — all applications') }, ...apps.map(a => ({ value: a.id, label: a.name }))];
  const appOptions = apps.map(a => ({ value: a.id, label: a.name }));

  const scopedRoles = useMemo(() => roles.filter(r => inScope(r.applicationTypeId)), [roles, scope]); // eslint-disable-line react-hooks/exhaustive-deps
  const tree = useMemo(() => buildMenuTree(menus.filter(m => inScope(m.applicationTypeId))), [menus, scope]); // eslint-disable-line react-hooks/exhaustive-deps
  const scopeTag = (appId?: number | null) => { const a = apps.find(x => x.id === appId); return a ? <Badge tone="info">{a.name}</Badge> : <span className="muted-text">{t('Shared')}</span>; };
  const typeName = (id?: number | null) => menuTypes.find(x => x.id === id)?.name ?? '';

  const saveMenu = async () => {
    if (!menuEdit) return;
    setSaving(true);
    try {
      if (menuEdit.id) await menusApi.update(menuEdit.id, menuEdit.form); else await menusApi.create(menuEdit.form);
      setMenuEdit(null); toast.success(t('Saved')); await load();
    } catch (e) { toast.error((e as Error).message); } finally { setSaving(false); }
  };
  const saveRole = async () => {
    if (!roleEdit) return;
    setSaving(true);
    try {
      if (roleEdit.id) await rolesApi.update(roleEdit.id, roleEdit.form); else await rolesApi.create(roleEdit.form);
      setRoleEdit(null); toast.success(t('Saved')); await load();
    } catch (e) { toast.error((e as Error).message); } finally { setSaving(false); }
  };
  const remove = async () => {
    if (!deleting) return;
    setSaving(true);
    try { await (deleting.kind === 'menu' ? menusApi.remove(deleting.id) : rolesApi.remove(deleting.id)); setDeleting(null); await load(); }
    catch (e) { toast.error((e as Error).message); } finally { setSaving(false); }
  };

  const newMenu = () => setMenuEdit({ id: null, form: { code: '', title: '', path: '', icon: '', displayOrder: 0, menuTypeId: menuTypes[0]?.id ?? null, applicationTypeId: scopeAppId, parentId: null, permissionId: null, isActive: true } });
  const newRole = () => setRoleEdit({ id: null, form: { name: '', code: '', description: '', applicationTypeId: scopeAppId, isActive: true } });

  const menuForm = menuEdit?.form;
  const setMenu = (patch: Partial<MenuWrite>) => setMenuEdit(m => (m ? { ...m, form: { ...m.form, ...patch } } : m));
  const roleForm = roleEdit?.form;
  const setRole = (patch: Partial<RoleWrite>) => setRoleEdit(r => (r ? { ...r, form: { ...r.form, ...patch } } : r));
  const num = (v: string) => (v ? Number(v) : null);

  return (
    <>
      <PageHeader title={t('Authorisation Studio')} subtitle={t('Role · Permission · Menu — by application scope')}
                  actions={<>
                    <span className="muted-text">{t('Scope')}</span>
                    <div style={{ minWidth: 240 }}>
                      <SelectField label="" value={scope} options={scopeOptions} onChange={e => setScope(e.target.value === 'shared' ? 'shared' : Number(e.target.value))} />
                    </div>
                    <Button text severity="secondary" icon="pi pi-refresh" aria-label={t('Refresh')} onClick={() => void load()} />
                  </>} />

      <div className="card">
        <TabView>
          {canMenus && (
            <TabPanel header={t('Menus')} leftIcon="pi pi-sitemap mr-2">
              <div className="toolbar">
                <span className="muted-text">{t('Menu tree within the {scope} scope', { scope: scopeLabel })}</span>
                <div className="grow" />
                <Button size="small" icon="pi pi-plus" label={t('New menu')} onClick={newMenu} />
              </div>
              <TreeTable value={tree} loading={loading} tableStyle={{ minWidth: 760 }}>
                <Column expander header={t('Name')} body={n => <span><b>{n.data.title}</b> <span className="muted-text">· {n.data.code}</span></span>} />
                <Column header={t('Route')} body={n => (n.data.path ? <code>{n.data.path}</code> : <span className="muted-text">—</span>)} />
                <Column header={t('Icon')} style={{ width: 70 }} body={n => (n.data.icon ? <i className={`pi pi-${n.data.icon}`} /> : null)} />
                <Column header={t('Type')} style={{ width: 110 }} body={n => typeName(n.data.menuTypeId)} />
                <Column header={t('Scope')} style={{ width: 120 }} body={n => scopeTag(n.data.applicationTypeId)} />
                <Column header="" style={{ width: 100 }} body={n => (
                  <div className="row">
                    <Button text rounded severity="secondary" icon="pi pi-pencil" aria-label={t('Edit')} onClick={() => setMenuEdit({ id: n.data.id, form: menuWrite(n.data) })} />
                    <Button text rounded severity="danger" icon="pi pi-trash" aria-label={t('Delete')} onClick={() => setDeleting({ kind: 'menu', id: n.data.id, name: n.data.title })} />
                  </div>
                )} />
              </TreeTable>
            </TabPanel>
          )}

          <TabPanel header={t('Roles')} leftIcon="pi pi-shield mr-2">
            <div className="toolbar">
              <span className="muted-text">{t('Roles within the {scope} scope — open a role to assign permissions', { scope: scopeLabel })}</span>
              <div className="grow" />
              {canRoles && <Button size="small" icon="pi pi-plus" label={t('New role')} onClick={newRole} />}
            </div>
            <PrimeTable value={scopedRoles} loading={loading} size="small" onRowClick={e => navigate(`/admin/roles/${(e.data as RoleRead).id}`)} rowClassName={() => 'cursor-pointer'} emptyMessage={t('No roles in this scope.')}>
              <Column field="name" header={t('Role')} body={(r: RoleRead) => <b>{r.name}</b>} />
              <Column field="code" header={t('Code')} body={(r: RoleRead) => (r.code ? <code>{r.code}</code> : <span className="muted-text">—</span>)} />
              <Column field="description" header={t('Description')} />
              <Column header={t('Scope')} style={{ width: 130 }} body={(r: RoleRead) => scopeTag(r.applicationTypeId)} />
              <Column header="" style={{ width: 140 }} body={(r: RoleRead) => (
                <div className="row">
                  <Button text rounded severity="info" icon="pi pi-key" aria-label={t('Edit permissions')} onClick={e => { e.stopPropagation(); navigate(`/admin/roles/${r.id}`); }} />
                  {canRoles && <Button text rounded severity="secondary" icon="pi pi-pencil" aria-label={t('Edit')} onClick={e => { e.stopPropagation(); setRoleEdit({ id: r.id, form: roleWrite(r) }); }} />}
                  {canRoles && <Button text rounded severity="danger" icon="pi pi-trash" aria-label={t('Delete')} onClick={e => { e.stopPropagation(); setDeleting({ kind: 'role', id: r.id, name: r.name }); }} />}
                </div>
              )} />
            </PrimeTable>
          </TabPanel>

          <TabPanel header={t('Permissions')} leftIcon="pi pi-key mr-2">
            <p className="muted-text" style={{ maxWidth: 620 }}>{t('The permission catalogue is global; it binds to an application through role assignments. To grant a permission, open the role from the Roles tab.')}</p>
            <PermissionCatalogue permissions={perms} loading={loading} />
          </TabPanel>

          <TabPanel header={t('Matrix')} leftIcon="pi pi-table mr-2">
            <PermissionMatrix roles={scopedRoles} permissions={perms} grants={grants} />
          </TabPanel>
        </TabView>
      </div>

      {menuEdit && menuForm && (
        <Modal title={menuEdit.id ? t('Edit menu') : t('New menu')} onClose={() => setMenuEdit(null)} width={560}
               footer={<><Button text severity="secondary" label={t('Cancel')} onClick={() => setMenuEdit(null)} disabled={saving} /><Button label={t('Save')} loading={saving} disabled={!menuForm.title.trim() || !menuForm.code.trim()} onClick={() => void saveMenu()} /></>}>
          <div className="form-grid">
            <TextField label={t('Name')} required value={menuForm.title} onChange={e => setMenu({ title: e.target.value })} />
            <TextField label={t('Code')} required value={menuForm.code} onChange={e => setMenu({ code: e.target.value.toUpperCase() })} />
            <TextField label={t('Route')} value={menuForm.path ?? ''} onChange={e => setMenu({ path: e.target.value || null })} />
            <TextField label={t('Icon')} value={menuForm.icon ?? ''} hint={t('A PrimeIcons name, e.g. users')} onChange={e => setMenu({ icon: e.target.value || null })} />
            <SelectField label={t('Type')} placeholder="—" value={menuForm.menuTypeId} options={menuTypes.map(x => ({ value: x.id, label: x.name }))} onChange={e => setMenu({ menuTypeId: num(e.target.value) })} />
            <SelectField label={t('Parent menu')} placeholder={t('Root menu')} value={menuForm.parentId} options={menus.filter(m => m.id !== menuEdit.id && inScope(m.applicationTypeId)).map(m => ({ value: m.id, label: m.title }))} onChange={e => setMenu({ parentId: num(e.target.value) })} />
            <SelectField label={t('Application (scope)')} placeholder={t('Shared (all applications)')} value={menuForm.applicationTypeId} options={appOptions} onChange={e => setMenu({ applicationTypeId: num(e.target.value) })} />
            <SelectField label={t('Required permission')} placeholder={t('None')} value={menuForm.permissionId} options={perms.map(p => ({ value: p.id, label: p.code }))} onChange={e => setMenu({ permissionId: num(e.target.value) })} />
            <TextField label={t('Order')} type="number" value={String(menuForm.displayOrder)} onChange={e => setMenu({ displayOrder: Number(e.target.value) || 0 })} />
          </div>
        </Modal>
      )}

      {roleEdit && roleForm && (
        <Modal title={roleEdit.id ? t('Edit role') : t('New role')} onClose={() => setRoleEdit(null)} width={520}
               footer={<><Button text severity="secondary" label={t('Cancel')} onClick={() => setRoleEdit(null)} disabled={saving} /><Button label={t('Save')} loading={saving} disabled={!roleForm.name.trim()} onClick={() => void saveRole()} /></>}>
          <div className="form-grid">
            <TextField label={t('Role')} required value={roleForm.name} onChange={e => setRole({ name: e.target.value })} />
            <TextField label={t('Code')} value={roleForm.code ?? ''} onChange={e => setRole({ code: e.target.value.toUpperCase() || null })} />
            <TextField wide label={t('Description')} value={roleForm.description ?? ''} onChange={e => setRole({ description: e.target.value || null })} />
            <SelectField label={t('Application (scope)')} placeholder={t('Shared (all applications)')} value={roleForm.applicationTypeId} options={appOptions} onChange={e => setRole({ applicationTypeId: num(e.target.value) })} />
            <CheckField label={t('Active')} checked={roleForm.isActive ?? true} onChange={v => setRole({ isActive: v })} />
          </div>
        </Modal>
      )}

      {deleting && <ConfirmDialog title={t('Delete')} message={t('Delete {name}? It can be restored later.', { name: deleting.name })} confirmLabel={t('Delete')} danger busy={saving} onCancel={() => setDeleting(null)} onConfirm={() => void remove()} />}
    </>
  );
}
