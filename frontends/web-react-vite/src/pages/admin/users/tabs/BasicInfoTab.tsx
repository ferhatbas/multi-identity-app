import { useEffect, useState } from 'react';
import { Button } from 'primereact/button';
import { CheckField, SelectField, TextField } from '@/components/ui/Form';
import { useToast } from '@/components/ui/Toast';
import { ApiException } from '@/services/http/httpService';
import { usersApi, type UserCompanyRead, type UserRead } from '@/features/users/users.service';
import type { CompanyRead } from '@/features/tenants/organization.service';
import { useI18n } from '@/i18n';

interface Form { email: string; firstName: string; lastName: string; phoneNumber: string; activeCompanyId: number | null; isActive: boolean; shouldChange: boolean; languageCode: string }

const toForm = (u: UserRead): Form => ({
  email: u.email, firstName: u.firstName, lastName: u.lastName, phoneNumber: u.phoneNumber ?? '', activeCompanyId: u.activeCompanyId,
  isActive: u.isActive, shouldChange: u.shouldChangePasswordOnNextLogin, languageCode: u.languageCode ?? '',
});

export function BasicInfoTab({ user, companies, memberships, canEdit, onSaved, onDirtyChange }: {
  user: UserRead; companies: CompanyRead[]; memberships: UserCompanyRead[]; canEdit: boolean; onSaved: () => void; onDirtyChange: (dirty: boolean) => void;
}) {
  const { t } = useI18n();
  const toast = useToast();
  const [form, setForm] = useState<Form>(() => toForm(user));
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [saving, setSaving] = useState(false);

  useEffect(() => { setForm(toForm(user)); setErrors({}); }, [user]);
  const dirty = JSON.stringify(form) !== JSON.stringify(toForm(user));
  useEffect(() => { onDirtyChange(dirty); return () => onDirtyChange(false); }, [dirty, onDirtyChange]);
  useEffect(() => {
    if (!dirty) return;
    const warn = (e: BeforeUnloadEvent) => { e.preventDefault(); };
    window.addEventListener('beforeunload', warn);
    return () => window.removeEventListener('beforeunload', warn);
  }, [dirty]);

  const set = <K extends keyof Form>(key: K, value: Form[K]) => setForm(f => ({ ...f, [key]: value }));
  const memberCompanies = memberships.map(m => companies.find(c => c.id === m.companyId)).filter((c): c is CompanyRead => !!c);

  const save = async () => {
    setSaving(true);
    setErrors({});
    try {
      await usersApi.update(user.id, {
        userName: user.userName, email: form.email.trim(), firstName: form.firstName.trim(), lastName: form.lastName.trim(),
        phoneNumber: form.phoneNumber.trim() || null, languageCode: form.languageCode.trim() || null,
        activeCompanyId: form.activeCompanyId, isActive: form.isActive, shouldChangePasswordOnNextLogin: form.shouldChange,
      });
      toast.success(t('User saved'));
      onSaved();
    } catch (e) {
      if (e instanceof ApiException) {
        const next: Record<string, string> = {};
        for (const f of ['email', 'firstName', 'lastName', 'phoneNumber', 'languageCode', 'activeCompanyId']) { const m = e.fieldMessage(f); if (m) next[f] = m; }
        setErrors(next);
        if (Object.keys(next).length === 0) toast.error(e.message);
      } else toast.error((e as Error).message);
    } finally {
      setSaving(false);
    }
  };

  return (
    <div>
      <div className="form-grid" style={{ maxWidth: 780 }}>
        <TextField label={t('User name')} value={user.userName} disabled hint={t('The user name cannot be changed: it is how the person is known.')} />
        <TextField label={t('E-mail')} type="email" required value={form.email} error={errors.email} disabled={!canEdit} onChange={e => set('email', e.target.value)} />
        <TextField label={t('First name')} required value={form.firstName} error={errors.firstName} disabled={!canEdit} onChange={e => set('firstName', e.target.value)} />
        <TextField label={t('Last name')} required value={form.lastName} error={errors.lastName} disabled={!canEdit} onChange={e => set('lastName', e.target.value)} />
        <TextField label={t('Phone')} value={form.phoneNumber} error={errors.phoneNumber} disabled={!canEdit} onChange={e => set('phoneNumber', e.target.value)} />
        <TextField label={t('Language')} value={form.languageCode} maxLength={8} disabled={!canEdit} hint={t('Empty follows the company.')} onChange={e => set('languageCode', e.target.value.toLowerCase())} />
        <SelectField label={t('Active company')} placeholder="—" value={form.activeCompanyId} error={errors.activeCompanyId} disabled={!canEdit}
                     options={memberCompanies.map(c => ({ value: c.id, label: c.name }))} hint={t('One of the companies the person belongs to.')}
                     onChange={e => set('activeCompanyId', e.target.value === '' ? null : Number(e.target.value))} />
        <div />
        <CheckField label={t('Active')} checked={form.isActive} disabled={!canEdit} onChange={v => set('isActive', v)} />
        <CheckField label={t('Must change the password at the next sign-in')} checked={form.shouldChange} disabled={!canEdit} onChange={v => set('shouldChange', v)} />
      </div>

      {canEdit && (
        <div className="save-bar" role="region" aria-label={t('Save')}>
          <span className="muted-text">{dirty ? t('You have unsaved changes.') : t('Nothing to save.')}</span>
          <Button text severity="secondary" label={t('Discard')} disabled={!dirty || saving} onClick={() => { setForm(toForm(user)); setErrors({}); }} />
          <Button label={saving ? t('Saving…') : t('Save')} disabled={!dirty || !form.email.trim() || !form.firstName.trim() || !form.lastName.trim()} loading={saving} onClick={() => void save()} />
        </div>
      )}
    </div>
  );
}
