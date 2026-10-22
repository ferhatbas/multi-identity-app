import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Button } from 'primereact/button';
import { RadioButton } from 'primereact/radiobutton';
import { CheckField, SelectField, TextField } from '@/components/ui/Form';
import { Modal } from '@/components/ui/Modal';
import { useToast } from '@/components/ui/Toast';
import { usersApi, type UserRead } from '@/features/users/users.service';
import { useI18n } from '@/i18n';

const name = (u: UserRead) => `${u.firstName} ${u.lastName}`.trim() || u.userName;

export function UserCopyDialog({ source, onClose }: { source: UserRead | null; onClose: () => void }) {
  const { t } = useI18n();
  const toast = useToast();
  const navigate = useNavigate();
  const [users, setUsers] = useState<UserRead[]>([]);
  const [sourceId, setSourceId] = useState<string>(source ? String(source.id) : '');
  const [form, setForm] = useState({ firstName: '', lastName: '', userName: '', email: '', phone: '' });
  const [copy, setCopy] = useState({ memberships: true, roles: true, permissions: true });
  const [strategy, setStrategy] = useState<'generated' | 'temporary'>('generated');
  const [password, setPassword] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [created, setCreated] = useState<{ id: number; userName: string; password: string } | null>(null);

  useEffect(() => { usersApi.paged({ pageSize: 200, orderBy: 'userName' }).then(p => setUsers(p.data)).catch(() => setUsers([])); }, []);
  const chosen = users.find(u => String(u.id) === sourceId) ?? source;
  const set = (patch: Partial<typeof form>) => setForm(f => ({ ...f, ...patch }));

  const submit = async () => {
    if (!chosen || !form.userName.trim() || !form.email.trim()) { setError(t('A source, a user name and an e-mail are required')); return; }
    if (strategy === 'temporary' && password.length < 8) { setError(t('The temporary password must be at least 8 characters')); return; }
    setBusy(true);
    setError(null);
    try {
      const result = await usersApi.copy(chosen.id, {
        userName: form.userName.trim(), email: form.email.trim(), firstName: form.firstName.trim() || null, lastName: form.lastName.trim() || null,
        password: strategy === 'temporary' ? password : null,
        copyMemberships: copy.memberships, copyRoles: copy.roles && copy.memberships, copyPermissions: copy.permissions && copy.memberships,
      });
      toast.success(t('User copied: {name}', { name: result.user.userName }));
      setCreated({ id: result.user.id, userName: result.user.userName, password: result.temporaryPassword });
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  };

  if (created) {
    return (
      <Modal title={t('Copy user')} onClose={() => { onClose(); navigate(`/admin/users/${created.id}`); }} width={560} compact
             footer={<Button label={t('Open the new user')} onClick={() => { onClose(); navigate(`/admin/users/${created.id}`); }} />}>
        <div className="stack">
          <div className="alert alert-success">{t('{name} was created.', { name: created.userName })}</div>
          <div className="alert alert-warning">{t('Temporary password (shown once):')} <code>{created.password}</code><div className="muted-text">{t('They must change it at the first sign-in.')}</div></div>
        </div>
      </Modal>
    );
  }

  return (
    <Modal title={t('Copy user')} onClose={onClose} width={700}
           footer={<>
             <Button outlined severity="secondary" label={t('Cancel')} onClick={onClose} disabled={busy} />
             <Button icon="pi pi-copy" label={t('Copy')} onClick={() => void submit()} disabled={busy || !chosen || !form.userName.trim() || !form.email.trim()} loading={busy} />
           </>}>
      <div className="stack">
        <section className="stack">
          <h3>{t('Source')}</h3>
          <SelectField label={t('User to copy')} value={sourceId} onChange={e => setSourceId(e.target.value)} placeholder="—"
                       options={users.map(u => ({ value: u.id, label: `${name(u)} (@${u.userName})` }))} />
          {chosen && <div className="muted-text">{chosen.email}{chosen.phoneNumber ? ` · ${chosen.phoneNumber}` : ''}</div>}
        </section>

        <section className="stack">
          <h3>{t('New user')}</h3>
          <div className="form-grid">
            <TextField label={t('First name')} value={form.firstName} onChange={e => set({ firstName: e.target.value })} hint={t('Empty keeps the source’s.')} />
            <TextField label={t('Last name')} value={form.lastName} onChange={e => set({ lastName: e.target.value })} />
            <TextField label={t('User name')} required value={form.userName} onChange={e => set({ userName: e.target.value.toLowerCase() })} />
            <TextField label={t('E-mail')} type="email" required value={form.email} onChange={e => set({ email: e.target.value.toLowerCase() })} />
          </div>
        </section>

        <section className="stack">
          <h3>{t('What to copy')}</h3>
          <CheckField label={t('Companies')} checked={copy.memberships} onChange={v => setCopy(c => ({ ...c, memberships: v }))} />
          <CheckField label={t('Roles')} checked={copy.roles && copy.memberships} disabled={!copy.memberships} onChange={v => setCopy(c => ({ ...c, roles: v }))} />
          <CheckField label={t('Extra and withdrawn permissions')} checked={copy.permissions && copy.memberships} disabled={!copy.memberships} onChange={v => setCopy(c => ({ ...c, permissions: v }))} />
        </section>

        <section className="stack">
          <h3>{t('First password')}</h3>
          <label className="check"><RadioButton checked={strategy === 'generated'} onChange={() => setStrategy('generated')} /><span>{t('Generate one and show it once')}</span></label>
          <label className="check"><RadioButton checked={strategy === 'temporary'} onChange={() => setStrategy('temporary')} /><span>{t('I choose a temporary password')}</span></label>
          {strategy === 'temporary' && <TextField label={t('Temporary password')} type="password" value={password} onChange={e => setPassword(e.target.value)} />}
        </section>

        {chosen && form.userName && <div className="alert alert-info">@{chosen.userName} → @{form.userName}</div>}
        {error && <div className="alert alert-error" role="alert">{error}</div>}
      </div>
    </Modal>
  );
}
