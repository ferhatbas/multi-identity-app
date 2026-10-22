import { useState, type FormEvent } from 'react';
import { Link, Navigate, useLocation, useNavigate, useSearchParams } from 'react-router-dom';
import { Button } from 'primereact/button';
import { Password } from 'primereact/password';
import { useAuth } from '@/features/auth/AuthContext';
import { ApiException } from '@/services/http/httpService';
import { useT } from '@/i18n';
import { Field, TextField } from '@/components/ui/Form';
import { AuthShell } from './AuthShell';

export function LoginPage() {
  const { user, signIn, completeTwoFactor } = useAuth();
  const t = useT();
  const navigate = useNavigate();
  const location = useLocation();
  const [params] = useSearchParams();
  const from = (location.state as { from?: string } | null)?.from ?? params.get('next') ?? '/';

  const [userName, setUserName] = useState('');
  const [password, setPassword] = useState('');
  const [code, setCode] = useState('');
  const [challenge, setChallenge] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  if (user) return <Navigate to={from} replace />;

  const fail = (e: unknown) => {
    setError(e instanceof ApiException && e.status === 0 ? t('The server cannot be reached') : (e as Error).message);
  };

  const submitCredentials = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      const result = await signIn(userName.trim(), password);
      if (result.kind === 'two-factor') setChallenge(result.twoFactorToken);
      else navigate(from, { replace: true });
    } catch (err) {
      fail(err);
    } finally {
      setBusy(false);
    }
  };

  const submitCode = async (e: FormEvent) => {
    e.preventDefault();
    if (!challenge) return;
    setBusy(true);
    setError(null);
    try {
      await completeTwoFactor(challenge, code.trim());
      navigate(from, { replace: true });
    } catch (err) {
      fail(err);
    } finally {
      setBusy(false);
    }
  };

  return (
    <AuthShell>
      {challenge === null ? (
        <>
          <h1>{t('Welcome back')}</h1>
          <p className="muted-text">{t('Sign in to continue.')}</p>
          <form onSubmit={submitCredentials}>
            {error && <div className="alert alert-error" role="alert">{error}</div>}
            <TextField label={t('User name or e-mail')} value={userName} onChange={e => setUserName(e.target.value)}
                       autoComplete="username" autoFocus required />
            <Field label={t('Password')} required>
              <Password value={password} onChange={e => setPassword(e.target.value)} feedback={false} toggleMask autoComplete="current-password"
                        inputClassName="w-full" className="w-full" />
            </Field>
            <Button type="submit" label={busy ? t('Signing in…') : t('Sign in')} loading={busy} disabled={!userName || !password} />
            <div className="auth-links"><Link to="/auth/forgot-password">{t('Forgot your password?')}</Link></div>
          </form>
        </>
      ) : (
        <>
          <h1>{t('Two-factor code')}</h1>
          <p className="muted-text">{t('Enter the 6-digit code from your authenticator app, or a recovery code.')}</p>
          <form onSubmit={submitCode}>
            {error && <div className="alert alert-error" role="alert">{error}</div>}
            <TextField label={t('Code')} value={code} onChange={e => setCode(e.target.value)} inputMode="text"
                       autoComplete="one-time-code" autoFocus required />
            <Button type="submit" label={busy ? t('Checking…') : t('Verify')} loading={busy} disabled={!code} />
            <Button type="button" text label={t('Use another account')} onClick={() => { setChallenge(null); setCode(''); setError(null); }} />
          </form>
        </>
      )}
    </AuthShell>
  );
}
