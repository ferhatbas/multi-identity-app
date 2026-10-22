import { useMemo, useState } from 'react';
import { Button } from 'primereact/button';
import { Checkbox } from 'primereact/checkbox';
import { MultiSelect } from 'primereact/multiselect';
import { Field, SelectField, TextField } from '@/components/ui/Form';
import { Modal } from '@/components/ui/Modal';
import type { ApplicationTypeRead, CompanyTypeRead } from '@/features/definitions/definitions.service';
import { menuStudioApi, type MenuRead, type StudioCreatedItem } from '@/features/menus/menus.service';
import type { PermissionRead, RoleRead } from '@/features/roles/roles.service';
import { MENU_ROUTES } from '@/routes/manifest';
import { useI18n } from '@/i18n';

const deriveCode = (route: string) => {
  const code = route.split('/').filter(p => p && !p.startsWith(':')).map(p => p.replace(/[^A-Za-z0-9]/g, '_').toUpperCase()).join('_');
  return code || 'HOME';
};

export function MenuStudioNewItemDialog({ menus, apps, companyTypes, roles, permissions, appId, typeId, canGrant, onClose, onCreated }: {
  menus: MenuRead[]; apps: ApplicationTypeRead[]; companyTypes: CompanyTypeRead[]; roles: RoleRead[]; permissions: PermissionRead[];
  appId: number | null; typeId: number | null; canGrant: boolean; onClose: () => void; onCreated: (created: StudioCreatedItem) => void;
}) {
  const { t } = useI18n();
  const [titleEn, setTitleEn] = useState('');
  const [titleTr, setTitleTr] = useState('');
  const [route, setRoute] = useState('');
  const [icon, setIcon] = useState('');
  const [parentId, setParentId] = useState<number | null>(null);
  const [application, setApplication] = useState<number | null>(null);
  const [permission, setPermission] = useState('');
  const [roleIds, setRoleIds] = useState<number[]>([]);
  const [typeIds, setTypeIds] = useState<number[]>(typeId !== null ? [typeId] : []);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');

  const groups = useMemo(() => menus.filter(m => !m.path && (m.applicationTypeId === null || m.applicationTypeId === appId)), [menus, appId]);
  const parent = groups.find(g => g.id === parentId) ?? null;
  const usedRoutes = useMemo(() => new Map(menus.filter(m => m.path).map(m => [m.path!, m.title])), [menus]);
  const entry = MENU_ROUTES.find(r => r.path === route);
  const permissionCode = permission.trim().toUpperCase();
  const known = new Set(permissions.map(p => p.code));
  const isNew = permissionCode !== '' && !known.has(permissionCode);

  const submit = async () => {
    setBusy(true); setError('');
    try {
      const created = await menuStudioApi.createItem({
        titleEn: titleEn.trim(), titleTr: titleTr.trim() || null, route, icon: icon.trim() || null, parentId, applicationTypeId: application ?? appId,
        permissionCode: permissionCode || null, grantToRoleIds: permissionCode ? roleIds : [], companyTypeIds: typeIds,
      });
      onCreated(created);
    } catch (e) { setError((e as Error).message); } finally { setBusy(false); }
  };

  return (
    <Modal title={parent ? t('New menu item — under {parent}', { parent: parent.title }) : t('New menu item')} onClose={() => { if (!busy) onClose(); }} width={680}
           footer={<><Button text severity="secondary" label={t('Cancel')} disabled={busy} onClick={onClose} />
                    <Button icon="pi pi-plus" label={t('Create and add to the menu')} loading={busy} disabled={!titleEn.trim() || !route} onClick={() => void submit()} /></>}>
      <div className="stack">
        {error && <div className="alert alert-error" role="alert">{error}</div>}
        <div className="form-grid">
          <TextField label={t('Name (English)')} required value={titleEn} hint={t('The fallback label.')} onChange={e => setTitleEn(e.target.value)} />
          <TextField label={t('Name (Turkish)')} value={titleTr} onChange={e => setTitleTr(e.target.value)} />
          <SelectField label={t('Route')} required placeholder={t('Pick a screen…')} value={route} options={MENU_ROUTES.map(r => ({ value: r.path, label: r.path }))}
                       hint={t('Only screens the app has are offered: a menu cannot open a page that does not exist.')}
                       onChange={e => { setRoute(e.target.value); const guard = MENU_ROUTES.find(r => r.path === e.target.value)?.permission[0]; if (guard && !permission) setPermission(guard); }} />
          <Field label={t('Code (automatic)')}><div className="p-inputtext p-component" style={{ display: 'flex', alignItems: 'center' }}><code>{route ? deriveCode(route) : '—'}</code></div></Field>
        </div>
        {route && usedRoutes.has(route) && <div className="alert alert-warning">{t('"{name}" already opens this screen; the new item will be a duplicate route.', { name: usedRoutes.get(route)! })}</div>}

        <div className="form-grid">
          <SelectField label={t('Parent group')} placeholder={t('Root (top level)')} value={parentId} options={groups.map(g => ({ value: g.id, label: g.title }))} onChange={e => setParentId(e.target.value ? Number(e.target.value) : null)} />
          <Field label={t('Module (read-only)')}><div className="p-inputtext p-component" style={{ display: 'flex', alignItems: 'center' }}>{parent?.moduleCode ?? t('none at the root')}</div></Field>
          <SelectField label={t('Application')} placeholder={t('Shared (all applications)')} value={application ?? parent?.applicationTypeId ?? null} options={apps.map(a => ({ value: a.id, label: a.name }))} onChange={e => setApplication(e.target.value ? Number(e.target.value) : null)} />
          <TextField label={t('Icon')} value={icon} hint={t('A PrimeIcons name, e.g. users')} onChange={e => setIcon(e.target.value)} />
        </div>

        <div className="form-grid">
          <TextField label={t('Permission (empty = open to everyone who reaches it)')} value={permission} onChange={e => setPermission(e.target.value)} placeholder="REPORT_VIEW"
                     hint={permissionCode ? (isNew ? t('{code} is not in the catalogue: it is created and given to the roles you pick. With no role nobody sees the item.', { code: permissionCode }) : t('{code} exists; it is reused.', { code: permissionCode })) : undefined} />
          <Field label={t('Give the permission to')}>
            <MultiSelect value={roleIds} options={roles.map(r => ({ label: r.name, value: r.id }))} display="chip" filter appendTo="self" disabled={!permissionCode || !canGrant}
                         placeholder={canGrant ? t('Roles…') : t('Needs the role-management permission')} onChange={e => setRoleIds((e.value ?? []) as number[])} />
          </Field>
        </div>
        {entry && permissionCode && entry.permission.length > 0 && !entry.permission.includes(permissionCode) && (
          <div className="alert alert-error">{t('The screen itself asks for {guard}: a role without it meets an error behind the menu.', { guard: entry.permission.join(', ') })}</div>
        )}

        <div>
          <strong>{t('Which company types')}</strong>
          <p className="muted-text">{t('With none ticked the entry is for every company type.')}</p>
          {companyTypes.map(ct => (
            <label key={ct.id} className="check"><Checkbox checked={typeIds.includes(ct.id)} onChange={e => setTypeIds(v => (e.checked ? [...v, ct.id] : v.filter(x => x !== ct.id)))} /><span>{ct.name}{ct.id === typeId ? <span className="muted-text"> · {t('the menu being edited')}</span> : null}</span></label>
          ))}
        </div>
      </div>
    </Modal>
  );
}
