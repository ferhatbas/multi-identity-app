import { useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Button } from 'primereact/button';
import type { MenuItem } from 'primereact/menuitem';
import { Avatar, RowMenu } from '@/components/ui/RowMenu';
import { DataTable, SearchBox, exportCsv, usePagedTable, type Column } from '@/components/ui/DataTable';
import { Badge, EmptyState, PageHeader, SelectField } from '@/components/ui/Form';
import { ConfirmDialog } from '@/components/ui/Modal';
import { useToast } from '@/components/ui/Toast';
import { useAuth } from '@/features/auth/AuthContext';
import { P } from '@/features/auth/permissions';
import { usersApi, type UserRead, type UserWrite } from '@/features/users/users.service';
import type { PageQuery, Paged } from '@/types/api';
import { useI18n } from '@/i18n';
import { ImpersonateUserDialog } from './dialogs/ImpersonateUserDialog';
import { UserCopyDialog } from './dialogs/UserCopyDialog';

type Status = '' | 'active' | 'inactive' | 'pending' | 'deleted';

const fullName = (u: UserRead) => `${u.firstName} ${u.lastName}`.trim() || u.userName;

const toWrite = (u: UserRead, patch: Partial<UserWrite>): UserWrite => ({
  userName: u.userName, email: u.email, firstName: u.firstName, lastName: u.lastName, phoneNumber: u.phoneNumber,
  languageCode: u.languageCode, activeCompanyId: u.activeCompanyId, isActive: u.isActive,
  shouldChangePasswordOnNextLogin: u.shouldChangePasswordOnNextLogin, ...patch,
});

export function UsersListPage() {
  const { t, formatDate } = useI18n();
  const toast = useToast();
  const navigate = useNavigate();
  const { user: me, can } = useAuth();
  const canWrite = can(P.UserManage);
  const [status, setStatus] = useState<Status>('');
  const [copyFrom, setCopyFrom] = useState<UserRead | 'new' | null>(null);
  const [viewAs, setViewAs] = useState<UserRead | null>(null);
  const [deleting, setDeleting] = useState<UserRead | null>(null);
  const [busy, setBusy] = useState(false);
  const [temporary, setTemporary] = useState<{ user: string; password: string } | null>(null);

  const fetcher = async (q: PageQuery): Promise<Paged<UserRead>> => {
    if (status === 'deleted') {
      const text = (q.search ?? '').toLowerCase();
      const all = (await usersApi.deleted()).filter(u => !text || `${u.userName} ${u.email} ${fullName(u)}`.toLowerCase().includes(text));
      return { pageNumber: 1, pageSize: all.length || 1, totalPages: 1, totalRecords: all.length, data: all };
    }
    const page = await usersApi.paged(q);
    const keep = (u: UserRead) => status === 'active' ? u.isActive : status === 'inactive' ? !u.isActive : status === 'pending' ? !u.emailConfirmed : true;
    return { ...page, data: page.data.filter(keep) };
  };

  const table = usePagedTable<UserRead>(fetcher, { orderBy: 'lastModifiedAt', desc: true, pageSize: 12 });
  const changeStatus = (next: Status) => { setStatus(next); setTimeout(() => table.setQuery({ pageNumber: 1 }), 0); };
  const deletedView = status === 'deleted';

  const run = async (work: () => Promise<unknown>, done: string) => {
    setBusy(true);
    try {
      await work();
      toast.success(done);
      table.reload();
    } catch (e) {
      toast.error((e as Error).message);
    } finally {
      setBusy(false);
    }
  };

  const menu = (u: UserRead): MenuItem[] => deletedView
    ? [{ label: t('Restore'), icon: 'pi pi-undo', command: () => void run(() => usersApi.restore(u.id), t('User restored')) }]
    : [
      { label: t('Edit'), icon: 'pi pi-pencil', command: () => navigate(`/admin/users/${u.id}`) },
      ...(canWrite ? [{ label: t('Copy user'), icon: 'pi pi-copy', command: () => setCopyFrom(u) }] : []),
      ...(canWrite && u.id !== me?.id ? [{
        label: t('Reset password'), icon: 'pi pi-key',
        command: () => void run(async () => { const r = await usersApi.resetPassword(u.id); setTemporary({ user: u.userName, password: r.temporaryPassword }); }, t('Password reset')),
      }] : []),
      ...(me?.isPlatformAdmin && u.id !== me.id ? [{ label: t('View as user'), icon: 'pi pi-eye', command: () => setViewAs(u) }] : []),
      ...(canWrite && u.id !== me?.id ? [
        { separator: true },
        {
          label: u.isActive ? t('Deactivate') : t('Activate'), icon: u.isActive ? 'pi pi-ban' : 'pi pi-check-circle',
          command: () => void run(() => usersApi.update(u.id, toWrite(u, { isActive: !u.isActive })), t('User saved')),
        },
        { label: t('Delete'), icon: 'pi pi-trash', className: 'menu-danger', command: () => setDeleting(u) },
      ] : []),
    ];

  const columns = useMemo<Column<UserRead>[]>(() => [
    {
      key: 'user', header: t('User'), sortKey: 'userName', csv: r => r.userName,
      render: r => (
        <div className="row">
          <Avatar id={r.id} text={fullName(r)} />
          <div><div className="cell-main">{fullName(r)}</div><div className="cell-sub mono">@{r.userName}</div></div>
        </div>
      ),
    },
    { key: 'email', header: t('E-mail'), sortKey: 'email', render: r => r.email, csv: r => r.email },
    { key: 'phone', header: t('Phone'), render: r => r.phoneNumber ?? '—', csv: r => r.phoneNumber },
    {
      key: 'status', header: t('Status'), csv: r => (!r.isActive ? 'inactive' : !r.emailConfirmed ? 'email pending' : 'active'),
      render: r => (!r.isActive ? <Badge>{t('Inactive')}</Badge> : !r.emailConfirmed ? <Badge tone="warning">{t('E-mail pending')}</Badge> : <Badge tone="success">{t('Active')}</Badge>),
    },
    { key: 'modified', header: t('Last modified'), sortKey: 'lastModifiedAt', render: r => formatDate(r.lastModifiedAt ?? r.createdAt), csv: r => r.lastModifiedAt },
  ], [t, formatDate]);

  return (
    <>
      <PageHeader
        title={t('Users')} subtitle={t('The people who can sign in.')}
        actions={canWrite && (
          <>
            <Button outlined severity="secondary" icon="pi pi-copy" label={t('Copy user')} onClick={() => setCopyFrom('new')} />
            <Button outlined severity="secondary" icon="pi pi-upload" label={t('Import')} onClick={() => navigate('/admin/users/import')} />
            <Button icon="pi pi-plus" label={t('New user')} onClick={() => navigate('/admin/users/new')} />
          </>
        )}
      />
      <div className="toolbar">
        <SearchBox onChange={table.setSearch} placeholder={t('Search name, user name or e-mail')} />
        <div style={{ width: 200 }}>
          <SelectField label="" value={status} onChange={e => changeStatus(e.target.value as Status)}
                       options={[
                         { value: '', label: t('All') }, { value: 'active', label: t('Active') }, { value: 'inactive', label: t('Inactive') },
                         { value: 'pending', label: t('E-mail pending') }, { value: 'deleted', label: t('Deleted') },
                       ]} />
        </div>
        <div className="grow" />
        <Button size="small" outlined severity="secondary" icon="pi pi-download" label={t('Export CSV')} disabled={table.rows.length === 0}
                onClick={() => exportCsv(columns, table.rows, 'users.csv')} />
        <Button text severity="secondary" icon="pi pi-refresh" aria-label={t('Refresh')} onClick={table.reload} />
      </div>

      {temporary && (
        <div className="alert alert-warning" style={{ marginBottom: 12 }}>
          {t('Temporary password of {name} (shown once):', { name: temporary.user })} <code>{temporary.password}</code>
          <Button text size="small" label={t('Close')} onClick={() => setTemporary(null)} />
        </div>
      )}

      <DataTable
        columns={columns} table={table} rowKey={r => r.id}
        rowClass={r => (r.isActive ? undefined : 'row-muted')}
        onRowClick={deletedView ? undefined : r => navigate(`/admin/users/${r.id}`)}
        emptyTitle={t('No users yet')}
        actions={r => (
          <>
            {!deletedView && <Button text rounded severity="secondary" icon="pi pi-pencil" aria-label={t('Edit')} onClick={() => navigate(`/admin/users/${r.id}`)} />}
            <RowMenu items={menu(r)} />
          </>
        )}
      />
      {!table.loading && table.rows.length === 0 && !table.query.search && status === '' && canWrite && (
        <div style={{ textAlign: 'center', marginTop: 12 }}><EmptyState title={t('Start by creating a new user.')} /><Button label={t('New user')} onClick={() => navigate('/admin/users/new')} /></div>
      )}

      {copyFrom && <UserCopyDialog source={copyFrom === 'new' ? null : copyFrom} onClose={() => setCopyFrom(null)} />}
      {viewAs && <ImpersonateUserDialog target={viewAs} onClose={() => setViewAs(null)} />}
      {deleting && (
        <ConfirmDialog
          title={t('Delete user')} message={t('Delete {name}? It can be restored later.', { name: fullName(deleting) })} confirmLabel={t('Delete')} danger busy={busy}
          onCancel={() => setDeleting(null)}
          onConfirm={() => void run(() => usersApi.remove(deleting.id), t('User deleted')).then(() => setDeleting(null))}
        />
      )}
    </>
  );
}
