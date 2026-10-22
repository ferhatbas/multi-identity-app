import { useEffect, useState } from 'react';
import { PageHeader } from '@/components/ui/Form';
import { useToast } from '@/components/ui/Toast';
import { rolesApi, type PermissionRead, type RolePermissionRead, type RoleRead } from '@/features/roles/roles.service';
import { useI18n } from '@/i18n';
import { PermissionCatalogue, PermissionMatrix } from './AccessParts';

export function PermissionsPage() {
  const { t } = useI18n();
  const toast = useToast();
  const [rows, setRows] = useState<PermissionRead[]>([]);
  const [loading, setLoading] = useState(true);
  useEffect(() => { rolesApi.permissions.list().then(setRows).catch(e => toast.error((e as Error).message)).finally(() => setLoading(false)); }, []); // eslint-disable-line react-hooks/exhaustive-deps
  return (
    <>
      <PageHeader title={t('Permissions')} subtitle={t('Everything that can be granted. Grant them to roles from the role page.')} />
      <PermissionCatalogue permissions={rows} loading={loading} />
    </>
  );
}

export function PermissionMatrixPage() {
  const { t } = useI18n();
  const toast = useToast();
  const [roles, setRoles] = useState<RoleRead[]>([]);
  const [perms, setPerms] = useState<PermissionRead[]>([]);
  const [grants, setGrants] = useState<RolePermissionRead[]>([]);
  useEffect(() => {
    Promise.all([rolesApi.list(), rolesApi.permissions.list(), rolesApi.grants.list()])
      .then(([r, p, g]) => { setRoles(r); setPerms(p); setGrants(g); }).catch(e => toast.error((e as Error).message));
  }, []); // eslint-disable-line react-hooks/exhaustive-deps
  return (
    <>
      <PageHeader title={t('Permission matrix')} subtitle={t('Which role can do what. Read-only.')} />
      <PermissionMatrix roles={roles} permissions={perms} grants={grants} />
    </>
  );
}
