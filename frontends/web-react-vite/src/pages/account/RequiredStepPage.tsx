import { useNavigate } from 'react-router-dom';
import { useAuth } from '@/features/auth/AuthContext';
import { authApi } from '@/features/auth/auth.service';
import { useI18n } from '@/i18n';
import { PageHeader } from '@/components/ui/Form';
import { useToast } from '@/components/ui/Toast';
import { ChangePasswordForm, TwoFactorPanel } from './AccountPanels';

export function RequiredStepPage() {
  const { user, signOut } = useAuth();
  const { t } = useI18n();
  const toast = useToast();
  const navigate = useNavigate();
  if (!user) return null;

  const finish = async () => {
    await signOut();
    toast.success(t('Done. Please sign in again.'));
    navigate('/auth/login');
  };

  return (
    <div style={{ maxWidth: 560, margin: '48px auto', padding: '0 16px' }}>
      <div className="row" style={{ justifyContent: 'space-between' }}>
        <PageHeader title={t('One more step')} subtitle={t('Your account needs this before you can continue.')} />
        <button className="p-button p-button-outlined p-button-secondary p-button-sm" onClick={() => void finish()}>{t('Sign out')}</button>
      </div>

      <div className="card card-pad">
        {user.mustChangePassword && (
          <>
            <h2 style={{ marginBottom: 14 }}>{t('Choose a new password')}</h2>
            <ChangePasswordForm onChanged={() => void finish()} />
          </>
        )}
        {!user.mustChangePassword && user.mfaSetupRequired && (
          <>
            <h2 style={{ marginBottom: 14 }}>{t('Set up two-factor sign-in')}</h2>
            <p className="muted-text" style={{ marginBottom: 14 }}>{t('Your role requires a second step at sign-in.')}</p>
            <TwoFactorPanel onChanged={() => void finish()} />
          </>
        )}
        {!user.mustChangePassword && !user.mfaSetupRequired && user.emailConfirmationRequired && (
          <>
            <h2 style={{ marginBottom: 14 }}>{t('Confirm your e-mail address')}</h2>
            <p className="muted-text" style={{ marginBottom: 14 }}>{t('We send a link to {email}. Open it, then sign in again.', { email: user.email })}</p>
            <button className="p-button" onClick={() => void authApi.sendConfirmation().then(() => toast.success(t('Confirmation e-mail sent'))).catch((e: Error) => toast.error(e.message))}>
              {t('Send the link')}
            </button>
          </>
        )}
      </div>
    </div>
  );
}
