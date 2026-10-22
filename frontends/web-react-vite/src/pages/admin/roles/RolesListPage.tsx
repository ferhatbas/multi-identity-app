import { useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Button } from 'primereact/button';
import type { MenuItem } from 'primereact/menuitem';
import { DataTable, SearchBox, exportCsv, usePagedTable, type Column } from '@/components/ui/DataTable';
import { Badge, PageHeader } from '@/components/ui/Form';
import { Avatar, RowMenu } from '@/components/ui/RowMenu';
import { useAuth } from '@/features/auth/AuthContext';
import { P } from '@/features/auth/permissions';
import { useOptions } from '@/features/lookups/useOptions';
import { rolesApi, type RoleRead } from '@/features/roles/roles.service';
import { useI18n } from '@/i18n';
import { RoleCopyDialog } from './RoleCopyDialog';

export function RolesListPage() {
  const { t } = useI18n();
  const navigate = useNavigate();
  const { can } = useAuth();
  const canManage = can(P.RoleManage);
  const table = usePagedTable<RoleRead>(rolesApi.paged, { orderBy: 'name', pageSize: 12 });
  const apps = useOptions(() => rolesApi.applications.list(), r => ({ value: r.id, label: r.name }));
  const [copy, setCopy] = useState<{ sourceId: number | null } | null>(null);
  const appName = (r: RoleRead) => apps.options.find(o => o.value === r.applicationTypeId)?.label;

  const columns = useMemo<Column<RoleRead>[]>(() => [
    { key: 'name', header: t('Role'), sortKey: 'name', csv: r => r.name, render: r => <div className="row"><Avatar id={r.id} text={r.name} /><span className="cell-main">{r.name}</span></div> },
    { key: 'code', header: t('Code'), sortKey: 'code', csv: r => r.code, render: r => (r.code ? <code>{r.code}</code> : '—') },
    { key: 'description', header: t('Description'), csv: r => r.description, render: r => r.description || '—' },
    { key: 'scope', header: t('Scope'), csv: r => appName(r) ?? 'Shared', render: r => (r.applicationTypeId ? <Badge tone="info">{appName(r) ?? `#${r.applicationTypeId}`}</Badge> : <span className="muted-text">{t('Shared')}</span>) },
    { key: 'status', header: t('Status'), csv: r => r.isActive, render: r => (r.isActive ? <Badge tone="success">{t('Active')}</Badge> : <Badge>{t('Inactive')}</Badge>) },
  // eslint-disable-next-line react-hooks/exhaustive-deps
  ], [t, apps.options]);

  const menu = (r: RoleRead): MenuItem[] => [
    { label: t('Edit'), icon: 'pi pi-pencil', command: () => navigate(`/admin/roles/${r.id}`) },
    ...(canManage ? [{ label: t('Copy'), icon: 'pi pi-copy', command: () => setCopy({ sourceId: r.id }) }] : []),
  ];

  return (
    <>
      <PageHeader title={t('Roles')} subtitle={t('Bundles of permissions you give to people.')}
                  actions={canManage && <Button outlined severity="secondary" icon="pi pi-copy" label={t('Copy role')} onClick={() => setCopy({ sourceId: null })} />} />
      <div className="toolbar">
        <SearchBox onChange={table.setSearch} placeholder={t('Search by name or code…')} />
        <div className="grow" />
        <Button size="small" outlined severity="secondary" icon="pi pi-download" label={t('Export CSV')} disabled={table.rows.length === 0} onClick={() => exportCsv(columns, table.rows, 'roles.csv')} />
        <Button text severity="secondary" icon="pi pi-refresh" aria-label={t('Refresh')} onClick={table.reload} />
      </div>
      <DataTable columns={columns} table={table} rowKey={r => r.id} rowClass={r => (r.isActive ? undefined : 'row-muted')} onRowClick={r => navigate(`/admin/roles/${r.id}`)}
                 emptyTitle={t('No roles yet')}
                 actions={r => <><Button text rounded severity="secondary" icon="pi pi-pencil" aria-label={t('Edit')} onClick={() => navigate(`/admin/roles/${r.id}`)} /><RowMenu items={menu(r)} /></>} />
      {copy && <RoleCopyDialog sourceId={copy.sourceId} onClose={() => setCopy(null)} onCreated={r => { setCopy(null); table.reload(); navigate(`/admin/roles/${r.id}`); }} />}
    </>
  );
}
