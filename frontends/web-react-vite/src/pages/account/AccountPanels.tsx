import QRCode from 'qrcode';
import { Column as PrimeColumn } from 'primereact/column';
import { DataTable as PrimeTable } from 'primereact/datatable';
import { useCallback, useEffect, useState, type FormEvent } from 'react';
import { authApi, type SessionRead, type TwoFactorSetup } from '@/features/auth/auth.service';
import { useAuth } from '@/features/auth/AuthContext';
import { notificationsApi, type PreferenceView } from '@/features/notifications/notifications.service';
import { useI18n } from '@/i18n';
import { Badge, CheckField, EmptyState, Spinner, TextField } from '@/components/ui/Form';
import { Icon } from '@/components/ui/Icon';
import { useToast } from '@/components/ui/Toast';

export function ChangePasswordForm({ onChanged }: { onChanged: () => void }) {
  const { t } = useI18n();
  const toast = useToast();
  const [current, setCurrent] = useState('');
  const [next, setNext] = useState('');
  const [again, setAgain] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (next !== again) { setError(t('The passwords do not match')); return; }
    setBusy(true);
    setError(null);
    try {
      await authApi.changePassword(current, next);
      toast.success(t('Password changed'));
      setCurrent(''); setNext(''); setAgain('');
      onChanged();
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setBusy(false);
    }
  };

  return (
    <form className="stack" onSubmit={submit} style={{ maxWidth: 420 }}>
      {error && <div className="alert alert-error" role="alert">{error}</div>}
      <TextField label={t('Current password')} type="password" value={current} onChange={e => setCurrent(e.target.value)} autoComplete="current-password" required />
      <TextField label={t('New password')} type="password" value={next} onChange={e => setNext(e.target.value)} autoComplete="new-password" required />
      <TextField label={t('Repeat the new password')} type="password" value={again} onChange={e => setAgain(e.target.value)} autoComplete="new-password" required />
      <div><button className="p-button" disabled={busy || !current || !next}>{busy ? t('Saving…') : t('Change password')}</button></div>
    </form>
  );
}

function RecoveryCodesView({ codes }: { codes: string[] }) {
  const { t } = useI18n();
  return (
    <div className="stack">
      <div className="alert alert-warning">{t('Save these recovery codes now. Each works once, and they are never shown again.')}</div>
      <div className="recovery">{codes.map(c => <span key={c}>{c}</span>)}</div>
      <div>
        <button className="p-button p-button-outlined p-button-secondary p-button-sm" onClick={() => void navigator.clipboard?.writeText(codes.join('\n'))}>{t('Copy')}</button>
      </div>
    </div>
  );
}

function Qr({ value }: { value: string }) {
  const [src, setSrc] = useState<string | null>(null);
  useEffect(() => { QRCode.toDataURL(value, { margin: 1, width: 192 }).then(setSrc).catch(() => setSrc(null)); }, [value]);
  return src ? <img className="qr" src={src} width={192} height={192} alt="" /> : null;
}

export function TwoFactorPanel({ onChanged }: { onChanged: () => void }) {
  const { user, reload } = useAuth();
  const { t } = useI18n();
  const toast = useToast();
  const [setup, setSetup] = useState<TwoFactorSetup | null>(null);
  const [code, setCode] = useState('');
  const [password, setPassword] = useState('');
  const [codes, setCodes] = useState<string[] | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const run = async (work: () => Promise<void>) => {
    setBusy(true);
    setError(null);
    try { await work(); } catch (e) { setError((e as Error).message); } finally { setBusy(false); }
  };

  if (codes) {
    return (
      <div className="stack" style={{ maxWidth: 480 }}>
        <div className="alert alert-success">{t('Two-factor sign-in is on.')}</div>
        <RecoveryCodesView codes={codes} />
        <div><button className="p-button" onClick={() => { setCodes(null); onChanged(); }}>{t('I have saved them')}</button></div>
      </div>
    );
  }

  if (user?.twoFactorEnabled) {
    return (
      <div className="stack" style={{ maxWidth: 420 }}>
        <div className="row"><Badge tone="success">{t('On')}</Badge><span className="muted-text">{t('Your sign-in asks for a code from your authenticator app.')}</span></div>
        {error && <div className="alert alert-error" role="alert">{error}</div>}
        <TextField label={t('Password')} type="password" value={password} onChange={e => setPassword(e.target.value)} autoComplete="current-password" />
        <TextField label={t('Code from the app or a recovery code')} value={code} onChange={e => setCode(e.target.value)} autoComplete="one-time-code" />
        <div className="row row-wrap">
          <button className="p-button p-button-outlined p-button-secondary" disabled={busy || !password || !code}
                  onClick={() => void run(async () => { setCodes((await authApi.newRecoveryCodes(password, code)).codes); setPassword(''); setCode(''); })}>
            {t('Get new recovery codes')}
          </button>
          <button className="p-button p-button-danger" disabled={busy || !password || !code}
                  onClick={() => void run(async () => { await authApi.twoFactorDisable(password, code); toast.success(t('Two-factor sign-in is off')); setPassword(''); setCode(''); await reload(); onChanged(); })}>
            {t('Turn off')}
          </button>
        </div>
      </div>
    );
  }

  if (!setup) {
    return (
      <div className="stack" style={{ maxWidth: 480 }}>
        <div className="row"><Badge>{t('Off')}</Badge><span className="muted-text">{t('Add a second step to your sign-in with an authenticator app.')}</span></div>
        {error && <div className="alert alert-error" role="alert">{error}</div>}
        <div><button className="p-button" disabled={busy} onClick={() => void run(async () => setSetup(await authApi.twoFactorSetup()))}>{t('Set up')}</button></div>
      </div>
    );
  }

  return (
    <div className="stack" style={{ maxWidth: 480 }}>
      <ol className="muted-text" style={{ margin: 0, paddingLeft: 18 }}>
        <li>{t('Scan the code with your authenticator app, or type the key.')}</li>
        <li>{t('Enter the 6-digit code it shows.')}</li>
      </ol>
      <Qr value={setup.otpAuthUri} />
      <div className="card card-pad">
        <div className="muted-text">{t('Or type this key')}</div>
        <div className="mono" style={{ fontSize: 16, letterSpacing: '0.08em', wordBreak: 'break-all' }}>{setup.secret.replace(/(.{4})/g, '$1 ').trim()}</div>
        <a href={setup.otpAuthUri} style={{ display: 'inline-block', marginTop: 8 }}>{t('Open in an authenticator app')}</a>
      </div>
      {error && <div className="alert alert-error" role="alert">{error}</div>}
      <TextField label={t('Code')} value={code} onChange={e => setCode(e.target.value)} inputMode="numeric" autoComplete="one-time-code" />
      <div className="row">
        <button className="p-button" disabled={busy || code.length < 6}
                onClick={() => void run(async () => { const result = await authApi.twoFactorEnable(code); setCodes(result.codes); setSetup(null); setCode(''); await reload(); })}>
          {t('Turn on')}
        </button>
        <button className="p-button p-button-outlined p-button-secondary" onClick={() => { setSetup(null); setCode(''); }}>{t('Cancel')}</button>
      </div>
    </div>
  );
}

export function SessionsPanel() {
  const { t, formatDate } = useI18n();
  const toast = useToast();
  const [sessions, setSessions] = useState<SessionRead[] | null>(null);

  const load = useCallback(() => { authApi.sessions().then(setSessions).catch((e: Error) => toast.error(e.message)); }, [toast]);
  useEffect(load, [load]);

  if (!sessions) return <Spinner label={t('Loading…')} />;
  const open = sessions.filter(s => s.isOpen);
  if (open.length === 0) return <EmptyState title={t('No open sessions')} />;

  return (
    <div className="stack">
      {open.map(s => (
        <div key={s.id} className="card card-pad row">
          <Icon name="globe" size={20} />
          <div className="grow">
            <div className="cell-main">{s.userAgent ?? t('Unknown device')} {s.isCurrent && <Badge tone="success">{t('This session')}</Badge>}</div>
            <div className="cell-sub">{s.ipAddress ?? '—'} · {t('signed in')} {formatDate(s.createdAt)} · {t('last seen')} {formatDate(s.lastSeenAt)}</div>
          </div>
          {!s.isCurrent && (
            <button className="p-button p-button-outlined p-button-secondary p-button-sm" onClick={() => void authApi.revokeSession(s.id).then(() => { toast.success(t('Session ended')); load(); }).catch((e: Error) => toast.error(e.message))}>
              {t('End')}
            </button>
          )}
        </div>
      ))}
      {open.length > 1 && (
        <div><button className="p-button p-button-outlined p-button-secondary" onClick={() => void authApi.logoutAll().then(() => { toast.success(t('Other sessions ended')); load(); })}>{t('End all other sessions')}</button></div>
      )}
    </div>
  );
}

const CATEGORY_LABEL: Record<string, string> = {
  SECURITY: 'Security notices', SYSTEM: 'System messages', ACTIVITY: 'Activity', MARKETING: 'News and offers',
};

export function NotificationsPanel() {
  const { t } = useI18n();
  const toast = useToast();
  const [prefs, setPrefs] = useState<PreferenceView[] | null>(null);

  useEffect(() => { notificationsApi.preferences().then(setPrefs).catch((e: Error) => toast.error(e.message)); }, [toast]);

  if (!prefs) return <Spinner label={t('Loading…')} />;
  const channels = [...new Set(prefs.map(p => p.channel))];
  const categories = [...new Set(prefs.map(p => p.category))];

  const toggle = async (p: PreferenceView, enabled: boolean) => {
    try {
      setPrefs(await notificationsApi.save([{ category: p.category, channel: p.channel, enabled }]));
    } catch (e) {
      toast.error((e as Error).message);
    }
  };

  return (
    <div className="card table-card" style={{ maxWidth: 560 }}>
      <PrimeTable value={categories.map(cat => ({ cat }))} size="small">
        <PrimeColumn header={t('Notification')} body={(row: { cat: string }) => <span className="cell-main">{t(CATEGORY_LABEL[row.cat] ?? row.cat)}</span>} />
        {channels.map(ch => (
          <PrimeColumn key={ch} header={ch === 'PUSH' ? t('Push') : t('E-mail')} body={(row: { cat: string }) => {
            const p = prefs.find(x => x.category === row.cat && x.channel === ch)!;
            return <CheckField label="" checked={p.enabled} disabled={p.mandatory} onChange={v => void toggle(p, v)} />;
          }} />
        ))}
      </PrimeTable>
      <div style={{ padding: '10px 14px', borderTop: '1px solid var(--border)' }}><span className="muted-text">{t('Security notices cannot be turned off.')}</span></div>
    </div>
  );
}
