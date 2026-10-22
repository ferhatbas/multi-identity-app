import { useEffect, useState, type FormEvent } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { authApi } from '@/features/auth/auth.service';
import { useT } from '@/i18n';
import { Spinner, TextField } from '@/components/ui/Form';
import { AuthShell } from './AuthShell';

export function ForgotPasswordPage() {
  const t = useT();
  const [email, setEmail] = useState('');
  const [busy, setBusy] = useState(false);
  const [sent, setSent] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      await authApi.forgotPassword(email.trim());
      setSent(true);
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setBusy(false);
    }
  };

  return (
    <AuthShell>
      <h1>{t('Forgot your password?')}</h1>
      {sent ? (
        <>
          <div className="alert alert-success" style={{ marginTop: 20 }}>
            {t('If that address belongs to an account, a link to choose a new password is on its way.')}
          </div>
          <div className="auth-links" style={{ marginTop: 20 }}><Link to="/auth/login">{t('Back to sign in')}</Link></div>
        </>
      ) : (
        <>
          <p className="muted-text">{t('Enter your e-mail address and we will send you a link.')}</p>
          <form onSubmit={submit}>
            {error && <div className="alert alert-error" role="alert">{error}</div>}
            <TextField label={t('E-mail')} type="email" value={email} onChange={e => setEmail(e.target.value)} autoFocus required />
            <button className="p-button p-button-block" disabled={busy || !email}>{busy ? t('Sending…') : t('Send the link')}</button>
            <div className="auth-links"><Link to="/auth/login">{t('Back to sign in')}</Link></div>
          </form>
        </>
      )}
    </AuthShell>
  );
}

export function ResetPasswordPage() {
  const t = useT();
  const [params] = useSearchParams();
  const token = params.get('token') ?? '';
  const [password, setPassword] = useState('');
  const [again, setAgain] = useState('');
  const [busy, setBusy] = useState(false);
  const [done, setDone] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (password !== again) { setError(t('The passwords do not match')); return; }
    setBusy(true);
    setError(null);
    try {
      await authApi.resetPassword(token, password);
      setDone(true);
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setBusy(false);
    }
  };

  return (
    <AuthShell>
      <h1>{t('Choose a new password')}</h1>
      {done ? (
        <>
          <div className="alert alert-success" style={{ marginTop: 20 }}>{t('Your password has been changed. Sign in with it.')}</div>
          <div className="auth-links" style={{ marginTop: 20 }}><Link to="/auth/login">{t('Go to sign in')}</Link></div>
        </>
      ) : !token ? (
        <div className="alert alert-error" style={{ marginTop: 20 }}>{t('This link is incomplete. Ask for a new one.')}</div>
      ) : (
        <form onSubmit={submit}>
          {error && <div className="alert alert-error" role="alert">{error}</div>}
          <TextField label={t('New password')} type="password" value={password} onChange={e => setPassword(e.target.value)}
                     autoComplete="new-password" autoFocus required />
          <TextField label={t('Repeat the new password')} type="password" value={again} onChange={e => setAgain(e.target.value)}
                     autoComplete="new-password" required />
          <button className="p-button p-button-block" disabled={busy || !password}>{busy ? t('Saving…') : t('Change password')}</button>
        </form>
      )}
    </AuthShell>
  );
}

export function ConfirmEmailPage() {
  const t = useT();
  const [params] = useSearchParams();
  const token = params.get('token') ?? '';
  const [state, setState] = useState<'working' | 'ok' | 'failed'>(token ? 'working' : 'failed');
  const [message, setMessage] = useState('');

  useEffect(() => {
    if (!token) return;
    authApi.confirmEmail(token).then(() => setState('ok')).catch((e: Error) => { setMessage(e.message); setState('failed'); });
  }, [token]);

  return (
    <AuthShell>
      <h1>{t('E-mail confirmation')}</h1>
      <div style={{ marginTop: 20 }}>
        {state === 'working' && <Spinner label={t('Checking…')} />}
        {state === 'ok' && <div className="alert alert-success">{t('Your e-mail address is confirmed.')}</div>}
        {state === 'failed' && <div className="alert alert-error">{message || t('This link is invalid or has expired.')}</div>}
      </div>
      <div className="auth-links" style={{ marginTop: 20 }}><Link to="/auth/login">{t('Go to sign in')}</Link></div>
    </AuthShell>
  );
}
