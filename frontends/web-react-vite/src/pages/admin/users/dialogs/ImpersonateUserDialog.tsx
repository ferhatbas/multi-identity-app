import { useState } from 'react';
import { Button } from 'primereact/button';
import { InputTextarea } from 'primereact/inputtextarea';
import { Field } from '@/components/ui/Form';
import { Modal } from '@/components/ui/Modal';
import { setTokens } from '@/features/auth/session';
import { usersApi, type UserRead } from '@/features/users/users.service';
import { useI18n } from '@/i18n';

export function ImpersonateUserDialog({ target, onClose }: { target: UserRead; onClose: () => void }) {
  const { t } = useI18n();
  const [reason, setReason] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const go = async () => {
    if (!reason.trim()) { setError(t('A reason is required for the audit trail')); return; }
    setBusy(true);
    setError(null);
    try {
      const response = await usersApi.impersonate(target.id, reason.trim());
      setTokens(response.accessToken, response.refreshToken);
      window.location.assign('/');
    } catch (e) {
      setError((e as Error).message);
      setBusy(false);
    }
  };

  return (
    <Modal title={t('View as user')} onClose={onClose} width={480} compact
           footer={<>
             <Button outlined severity="secondary" label={t('Cancel')} onClick={onClose} disabled={busy} />
             <Button severity="warning" icon="pi pi-eye" label={t('View')} onClick={() => void go()} disabled={busy || !reason.trim()} loading={busy} />
           </>}>
      <div className="stack">
        <div className="alert alert-warning">
          {t('You will see the app through this user’s eyes: their menu and permissions. Everything you do is recorded against you in the audit trail. The session lasts as long as one access token.')}
        </div>
        <div className="row"><strong>{target.firstName} {target.lastName}</strong><span className="muted-text mono">@{target.userName}</span></div>
        <Field label={t('Reason (audit record)')} required>
          <InputTextarea rows={3} value={reason} onChange={e => setReason(e.target.value)} placeholder={t('e.g. checking what the role menus look like')} maxLength={512} autoFocus />
        </Field>
        {error && <div className="alert alert-error" role="alert">{error}</div>}
      </div>
    </Modal>
  );
}
