import { useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { Dropdown } from 'primereact/dropdown';
import { Button } from 'primereact/button';
import { useAuth } from '@/features/auth/AuthContext';
import { authApi } from '@/features/auth/auth.service';
import { UserTypeNames } from '@/types/api';
import { LANGUAGES, useI18n } from '@/i18n';
import { Badge, PageHeader } from '@/components/ui/Form';
import { TabBar } from '@/components/ui/Tabs';
import { useToast } from '@/components/ui/Toast';
import { ChangePasswordForm, NotificationsPanel, SessionsPanel, TwoFactorPanel } from './AccountPanels';

type Tab = 'profile' | 'password' | 'two-factor' | 'sessions' | 'notifications';

function LanguageRow() {
  const { t, language, setLanguage } = useI18n();
  const { user, reload } = useAuth();
  const toast = useToast();
  const [busy, setBusy] = useState(false);

  const choose = async (code: string | null) => {
    setBusy(true);
    try {
      const mine = await authApi.setLanguage(code);
      setLanguage(mine.effectiveLanguageCode);
      await reload();
      toast.success(t('Language saved'));
    } catch (e) { toast.error((e as Error).message); } finally { setBusy(false); }
  };

  return (
    <div className="row row-wrap">
      <Dropdown value={language} options={LANGUAGES.map(l => ({ label: l.label, value: l.code }))} disabled={busy} appendTo="self" style={{ width: 170 }}
                aria-label={t('Language')} onChange={e => void choose(e.value as string)} />
      {user?.languageCode
        ? <Button text size="small" icon="pi pi-replay" label={t('Use the organisation language')} disabled={busy} onClick={() => void choose(null)} />
        : <span className="muted-text">{t('Using the organisation default')}</span>}
    </div>
  );
}

export function AccountPage() {
  const { user, reload } = useAuth();
  const { t } = useI18n();
  const toast = useToast();
  const navigate = useNavigate();
  const [tab, setTab] = useState<Tab>(useLocation().pathname.endsWith('/security') ? 'two-factor' : 'profile');
  if (!user) return null;

  const tabs: { id: Tab; label: string }[] = [
    { id: 'profile', label: t('Profile') },
    { id: 'password', label: t('Password') },
    { id: 'two-factor', label: t('Two-factor') },
    { id: 'sessions', label: t('Sessions') },
    { id: 'notifications', label: t('Notifications') },
  ];

  return (
    <>
      <PageHeader title={t('My account')} subtitle={`${user.firstName} ${user.lastName} · ${user.userName}`} />
      <TabBar tabs={tabs} value={tab} onChange={next => { setTab(next); navigate(next === 'two-factor' ? '/account/security' : '/account', { replace: true }); }} />

      {tab === 'profile' && (
        <div className="card card-pad" style={{ maxWidth: 640 }}>
          <dl className="kv">
            <dt>{t('Name')}</dt><dd>{user.firstName} {user.lastName}</dd>
            <dt>{t('User name')}</dt><dd>{user.userName}</dd>
            <dt>{t('E-mail')}</dt>
            <dd>
              {user.email}{' '}
              {user.emailConfirmationRequired
                ? <button className="p-button p-button-outlined p-button-secondary p-button-sm" onClick={() => void authApi.sendConfirmation().then(() => toast.success(t('Confirmation e-mail sent'))).catch((e: Error) => toast.error(e.message))}>{t('Confirm')}</button>
                : null}
            </dd>
            <dt>{t('Language')}</dt><dd><LanguageRow /></dd>
            <dt>{t('Role')}</dt>
            <dd>{user.isPlatformAdmin ? <Badge tone="info">{t('Platform administrator')}</Badge> : <span>{t('Member')}</span>}</dd>
            <dt>{t('Companies')}</dt>
            <dd>
              {user.companies.length === 0 ? '—' : user.companies.map(c => (
                <div key={c.companyId}>{c.name} <span className="muted-text">· {UserTypeNames[c.membershipType] ?? c.membershipType}</span></div>
              ))}
            </dd>
          </dl>
        </div>
      )}
      {tab === 'password' && <ChangePasswordForm onChanged={() => void reload()} />}
      {tab === 'two-factor' && <TwoFactorPanel onChanged={() => void reload()} />}
      {tab === 'sessions' && <SessionsPanel />}
      {tab === 'notifications' && <NotificationsPanel />}
    </>
  );
}
