import type { ReactNode } from 'react';
import { Button } from 'primereact/button';
import { Dropdown } from 'primereact/dropdown';
import { LANGUAGES, useI18n } from '@/i18n';
import { Icon } from '@/components/ui/Icon';
import { useTheme } from '@/main-layout/ThemeContext';

export function AuthShell({ children }: { children: ReactNode }) {
  const { t, language, setLanguage } = useI18n();
  const { theme, toggle } = useTheme();

  return (
    <div className="auth">
      <div className="auth-top">
        <Dropdown value={language} options={LANGUAGES.map(l => ({ label: l.label, value: l.code }))} aria-label={t('Language')}
                  onChange={e => setLanguage(e.value as string)} style={{ width: 130 }} />
        <Button text severity="secondary" icon={theme === 'dark' ? 'pi pi-sun' : 'pi pi-moon'} aria-label={t('Switch theme')} onClick={toggle} />
      </div>
      <div className="auth-box card">
        <div className="auth-brand">
          <span className="brand-mark"><Icon name="lock" size={20} /></span>
          <strong>FSIdentity</strong>
        </div>
        {children}
      </div>
    </div>
  );
}
