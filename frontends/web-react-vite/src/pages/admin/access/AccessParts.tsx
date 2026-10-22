import { useMemo, useState } from 'react';
import { Column } from 'primereact/column';
import { DataTable as PrimeTable } from 'primereact/datatable';
import { Badge } from '@/components/ui/Form';
import { SearchBox } from '@/components/ui/DataTable';
import type { PermissionRead, RolePermissionRead, RoleRead } from '@/features/roles/roles.service';
import { UserTypeNames } from '@/types/api';
import { useI18n } from '@/i18n';

export function PermissionCatalogue({ permissions, loading }: { permissions: PermissionRead[]; loading?: boolean }) {
  const { t } = useI18n();
  const [search, setSearch] = useState('');
  const rows = useMemo(() => {
    const q = search.trim().toLowerCase();
    return q ? permissions.filter(p => p.name.toLowerCase().includes(q) || p.code.toLowerCase().includes(q) || (p.description ?? '').toLowerCase().includes(q)) : permissions;
  }, [permissions, search]);

  return (
    <>
      <div className="toolbar"><SearchBox onChange={setSearch} placeholder={t('Search permission…')} /></div>
      <PrimeTable value={rows} loading={loading} size="small" paginator rows={15} emptyMessage={t('No permissions')}>
        <Column field="name" header={t('Name')} body={(p: PermissionRead) => <b>{p.name}</b>} sortable />
        <Column field="code" header={t('Code')} body={(p: PermissionRead) => <code>{p.code}</code>} sortable />
        <Column field="description" header={t('Description')} body={(p: PermissionRead) => p.description || '—'} />
        <Column header={t('Kind')} body={(p: PermissionRead) => (p.isManagement
          ? <Badge tone="warning">{t('Management · {rank}', { rank: UserTypeNames[p.managementRank ?? 0] })}</Badge> : <Badge>{t('Ordinary')}</Badge>)} />
      </PrimeTable>
    </>
  );
}

export function PermissionMatrix({ roles, permissions, grants }: { roles: RoleRead[]; permissions: PermissionRead[]; grants: RolePermissionRead[] }) {
  const { t } = useI18n();
  const [search, setSearch] = useState('');
  const byRole = useMemo(() => {
    const map = new Map<number, Set<number>>();
    for (const g of grants) { if (!map.has(g.roleId)) map.set(g.roleId, new Set()); map.get(g.roleId)!.add(g.permissionId); }
    return map;
  }, [grants]);
  const rows = useMemo(() => {
    const q = search.trim().toLowerCase();
    return q ? permissions.filter(p => p.name.toLowerCase().includes(q) || p.code.toLowerCase().includes(q)) : permissions;
  }, [permissions, search]);

  return (
    <>
      <div className="toolbar">
        <SearchBox onChange={setSearch} placeholder={t('Search permission…')} />
        <span className="muted-text">{t('{p} permissions × {r} roles', { p: rows.length, r: roles.length })}</span>
      </div>
      {roles.length === 0 ? <div className="empty"><strong>{t('No roles in this scope.')}</strong></div> : (
        <div style={{ overflow: 'auto', maxHeight: 620 }}>
          <table className="access-matrix">
            <thead>
              <tr>
                <th className="sticky-col">{t('Permission')}</th>
                {roles.map(r => (
                  <th key={r.id} title={r.name}>
                    <div>{r.name}</div>
                    <div className="muted-text">{rows.filter(p => byRole.get(r.id)?.has(p.id)).length}</div>
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {rows.map(p => (
                <tr key={p.id}>
                  <th className="sticky-col"><code>{p.code}</code>{p.name !== p.code && <div className="muted-text">{p.name}</div>}</th>
                  {roles.map(r => <td key={r.id}>{byRole.get(r.id)?.has(p.id) ? <i className="pi pi-check" style={{ color: 'var(--brand, #2563eb)', fontSize: 12 }} /> : <span className="muted-text">·</span>}</td>)}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </>
  );
}
