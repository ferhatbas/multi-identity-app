import { useCallback, useEffect, useState } from 'react';
import { Button } from 'primereact/button';
import { Column as PrimeColumn } from 'primereact/column';
import { DataTable as PrimeTable } from 'primereact/datatable';
import { Badge, EmptyState, Spinner } from '@/components/ui/Form';
import { ConfirmDialog, Modal } from '@/components/ui/Modal';
import { useToast } from '@/components/ui/Toast';
import { authApi, type SessionRead } from '@/features/auth/auth.service';
import { useAuth } from '@/features/auth/AuthContext';
import { P } from '@/features/auth/permissions';
import { auditApi, type AuditLogRead } from '@/features/audit/audit.service';
import { usersApi, type UserRead } from '@/features/users/users.service';
import { NotificationsPanel, SessionsPanel, TwoFactorPanel } from '@/pages/account/AccountPanels';
import { useI18n } from '@/i18n';

export function SecurityTab({ user }: { user: UserRead }) {
  const { t, formatDate } = useI18n();
  const toast = useToast();
  const { user: me, can, reload } = useAuth();
  const canManage = can(P.UserManage);
  const [confirmReset, setConfirmReset] = useState(false);
  const [temporary, setTemporary] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const self = me?.id === user.id;

  const run = async (work: () => Promise<void>) => {
    setBusy(true);
    try { await work(); } catch (e) { toast.error((e as Error).message); } finally { setBusy(false); }
  };

  return (
    <div className="stack" style={{ maxWidth: 760 }}>
      <div className="card card-pad">
        <dl className="kv" style={{ gridTemplateColumns: '240px 1fr' }}>
          <dt>{t('Must change the password at the next sign-in')}</dt>
          <dd>{user.shouldChangePasswordOnNextLogin ? <Badge tone="warning">{t('Required')}</Badge> : <Badge>{t('No')}</Badge>}</dd>
          <dt>{t('Two-factor sign-in')}</dt><dd>{user.twoFactorEnabled ? <Badge tone="success">{t('On')}</Badge> : <Badge>{t('Off')}</Badge>}</dd>
          <dt>{t('E-mail')}</dt><dd>{user.emailConfirmed ? <Badge tone="success">{t('Confirmed')}</Badge> : <Badge tone="warning">{t('Pending')}</Badge>}</dd>
          <dt>{t('Last modified')}</dt><dd>{formatDate(user.lastModifiedAt ?? user.createdAt)}</dd>
        </dl>
      </div>

      {canManage && !self && (
        <div className="row row-wrap">
          <Button outlined severity="secondary" icon="pi pi-key" label={t('Reset password')} onClick={() => setConfirmReset(true)} disabled={busy} />
          <Button text severity="secondary" icon="pi pi-envelope" label={t('Send reset e-mail')} disabled={busy || !user.email}
                  onClick={() => void run(async () => { await authApi.forgotPassword(user.email); toast.success(t('If the address belongs to an account, a reset link is on its way.')); })} />
          <Button text severity="warning" icon="pi pi-lock-open" label={t('Unlock')} disabled={busy}
                  onClick={() => void run(async () => { await usersApi.unlock(user.id); toast.success(t('Account unlocked')); })} />
        </div>
      )}

      {confirmReset && (
        <ConfirmDialog title={t('Reset password')} message={t('A new temporary password is generated for {name}. They must change it at the next sign-in, and their sessions end.', { name: user.userName })}
                       confirmLabel={t('Reset')} busy={busy} onCancel={() => setConfirmReset(false)}
                       onConfirm={() => void run(async () => { const r = await usersApi.resetPassword(user.id); setTemporary(r.temporaryPassword); setConfirmReset(false); })} />
      )}
      {temporary && (
        <Modal title={t('Temporary password')} onClose={() => setTemporary(null)} width={460} compact footer={<Button label={t('OK')} onClick={() => setTemporary(null)} />}>
          <div className="alert alert-warning">{t('Shown once. Hand it over safely.')}</div>
          <pre className="recovery" style={{ marginTop: 12 }}>{temporary}</pre>
        </Modal>
      )}

      {self && (
        <>
          <h3>{t('Two-factor sign-in')}</h3>
          <TwoFactorPanel onChanged={() => void reload()} />
          <h3>{t('My sessions')}</h3>
          <SessionsPanel />
          <h3>{t('Notifications')}</h3>
          <NotificationsPanel />
        </>
      )}
    </div>
  );
}

export function SessionsTab({ user }: { user: UserRead }) {
  const { t, formatDate } = useI18n();
  const toast = useToast();
  const { can } = useAuth();
  const [sessions, setSessions] = useState<SessionRead[] | null>(null);
  const [ending, setEnding] = useState<SessionRead | 'all' | null>(null);
  const [busy, setBusy] = useState(false);
  const canManage = can(P.SessionManage);

  const load = useCallback(() => { usersApi.sessions(user.id).then(s => setSessions(s.filter(x => x.isOpen))).catch((e: Error) => toast.error(e.message)); }, [user.id, toast]);
  useEffect(load, [load]);

  const end = async () => {
    if (!ending) return;
    setBusy(true);
    try {
      if (ending === 'all') { const n = await usersApi.revokeAllSessions(user.id); toast.success(t('{n} sessions ended', { n })); }
      else { await usersApi.revokeSession(user.id, ending.id); toast.success(t('Session ended')); }
      setEnding(null);
      load();
    } catch (e) { toast.error((e as Error).message); } finally { setBusy(false); }
  };

  if (!sessions) return <Spinner label={t('Loading…')} />;

  return (
    <div className="stack" style={{ maxWidth: 800 }}>
      {canManage && sessions.length > 0 && (
        <div className="row" style={{ justifyContent: 'flex-end' }}>
          <Button outlined severity="danger" icon="pi pi-sign-out" label={t('Sign out everywhere')} onClick={() => setEnding('all')} />
        </div>
      )}
      {sessions.length === 0 && <EmptyState title={t('No open sessions')} />}
      {sessions.map(s => (
        <div key={s.id} className="card card-pad row">
          <i className="pi pi-desktop" />
          <div className="grow">
            <div className="cell-main">{s.userAgent ?? t('Unknown device')}</div>
            <div className="cell-sub">{s.ipAddress ?? '—'} · {t('last seen')} {formatDate(s.lastSeenAt)}</div>
          </div>
          {s.impersonatorUserId && <Badge tone="warning">{t('Acting as')}</Badge>}
          {canManage && <Button text severity="danger" size="small" icon="pi pi-times" label={t('Revoke')} onClick={() => setEnding(s)} />}
        </div>
      ))}
      {ending && (
        <ConfirmDialog title={ending === 'all' ? t('Sign out everywhere') : t('End this session')}
                       message={ending === 'all' ? t('End all sessions for this user and sign them out of every device?') : t('The device is signed out at once.')}
                       confirmLabel={t('End')} danger busy={busy} onCancel={() => setEnding(null)} onConfirm={() => void end()} />
      )}
    </div>
  );
}

const TONE = { Create: 'success', Update: 'info', Delete: 'danger', Restore: 'warning' } as const;

export function ActivityTab({ user }: { user: UserRead }) {
  const { t, formatDate } = useI18n();
  const [rows, setRows] = useState<AuditLogRead[] | null>(null);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);

  useEffect(() => {
    auditApi.paged({ pageNumber: page, pageSize: 15, userId: user.id }).then(p => { setRows(p.data); setTotal(p.totalRecords); }).catch(() => setRows([]));
  }, [user.id, page]);

  if (!rows) return <Spinner label={t('Loading…')} />;
  if (rows.length === 0 && page === 1) return <EmptyState title={t('Activity')} text={t('No records for this user.')} />;

  return (
    <PrimeTable value={rows} size="small" lazy paginator rows={15} first={(page - 1) * 15} totalRecords={total} onPage={e => setPage((e.page ?? 0) + 1)}
                paginatorTemplate="CurrentPageReport FirstPageLink PrevPageLink PageLinks NextPageLink LastPageLink" currentPageReportTemplate={t('{from}–{to} of {total}', { from: '{first}', to: '{last}', total: '{totalRecords}' })}>
      <PrimeColumn header={t('Action')} body={(r: AuditLogRead) => <Badge tone={(TONE as Record<string, 'success' | 'info' | 'danger' | 'warning'>)[r.action] ?? 'neutral'}>{r.action}</Badge>} />
      <PrimeColumn header={t('Record')} body={(r: AuditLogRead) => <><span className="cell-main">{r.entityName}</span> <span className="muted-text">#{r.entityId}</span></>} />
      <PrimeColumn header={t('Changed')} body={(r: AuditLogRead) => <code>{(r.changedFields ?? '').replace(/[[\]"]/g, '').replace(/,/g, ', ') || '—'}</code>} />
      <PrimeColumn header={t('Date')} style={{ width: 180 }} body={(r: AuditLogRead) => formatDate(r.createdAt)} />
    </PrimeTable>
  );
}
