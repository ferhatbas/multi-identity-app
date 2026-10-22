import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Button } from 'primereact/button';
import { MultiSelect } from 'primereact/multiselect';
import { RadioButton } from 'primereact/radiobutton';
import { Steps } from 'primereact/steps';
import { Badge, CheckField, EmptyState, Field, PageHeader, TextField } from '@/components/ui/Form';
import { Modal } from '@/components/ui/Modal';
import { SelectField } from '@/components/ui/Form';
import { useToast } from '@/components/ui/Toast';
import { rolesApi, type RoleRead } from '@/features/roles/roles.service';
import { companiesApi, type CompanyRead } from '@/features/tenants/organization.service';
import { unitsApi, type UnitRead } from '@/features/units/units.service';
import { usersApi } from '@/features/users/users.service';
import { UserType } from '@/types/api';
import { useI18n } from '@/i18n';

interface Basic { firstName: string; lastName: string; userName: string; email: string; phone: string }

function generatePassword(): string {
  const pick = (set: string) => set[crypto.getRandomValues(new Uint32Array(1))[0] % set.length];
  const all = 'abcdefghijkmnopqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789';
  const chars = [pick('abcdefghijkmnopqrstuvwxyz'), pick('ABCDEFGHJKLMNPQRSTUVWXYZ'), pick('23456789'), pick('!@#$%*-_?'), ...Array.from({ length: 12 }, () => pick(all))];
  return chars.sort(() => crypto.getRandomValues(new Uint32Array(1))[0] / 2 ** 32 - 0.5).join('');
}

export function UserCreateWizardPage() {
  const { t } = useI18n();
  const toast = useToast();
  const navigate = useNavigate();
  const [step, setStep] = useState(0);
  const [basic, setBasic] = useState<Basic>({ firstName: '', lastName: '', userName: '', email: '', phone: '' });
  const [strategy, setStrategy] = useState<'generated' | 'temporary'>('generated');
  const [password, setPassword] = useState('');
  const [companies, setCompanies] = useState<CompanyRead[]>([]);
  const [roles, setRoles] = useState<RoleRead[]>([]);
  const [units, setUnits] = useState<UnitRead[]>([]);
  const [selected, setSelected] = useState<number[]>([]);
  const [activeId, setActiveId] = useState<number | null>(null);
  const [roleIds, setRoleIds] = useState<Record<number, number[]>>({});
  const [unitIds, setUnitIds] = useState<number[]>([]);
  const [primaryUnit, setPrimaryUnit] = useState<number | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [done, setDone] = useState<{ id: number; userName: string; password: string; problems: string[] } | null>(null);

  useEffect(() => {
    companiesApi.list().then(setCompanies).catch(() => undefined);
    rolesApi.list().then(setRoles).catch(() => undefined);
    unitsApi.list().then(setUnits).catch(() => undefined);
  }, []);

  const set = (patch: Partial<Basic>) => setBasic(b => ({ ...b, ...patch }));
  const toggleCompany = (id: number, on: boolean) => {
    const next = on ? [...selected, id] : selected.filter(c => c !== id);
    setSelected(next);
    setActiveId(a => (on ? a ?? id : a === id ? next[0] ?? null : a));
    if (!on) setRoleIds(r => { const { [id]: _removed, ...rest } = r; return rest; });
  };
  const toggleRole = (company: number, role: number) =>
    setRoleIds(r => ({ ...r, [company]: (r[company] ?? []).includes(role) ? (r[company] ?? []).filter(x => x !== role) : [...(r[company] ?? []), role] }));

  const problems = useMemo(() => {
    if (step === 0) {
      const list: string[] = [];
      if (!basic.firstName.trim() || !basic.lastName.trim() || !basic.userName.trim() || !basic.email.trim()) list.push(t('First name, last name, user name and e-mail are required.'));
      else if (!basic.email.includes('@')) list.push(t('The e-mail must contain @.'));
      if (strategy === 'temporary' && password.length < 8) list.push(t('The temporary password must be at least 8 characters.'));
      return list;
    }
    if (step === 1) return selected.length === 0 ? [t('Choose at least one company.')] : [];
    if (step === 2) return selected.filter(c => !(roleIds[c]?.length)).map(c => t('{company} needs at least one role.', { company: companies.find(x => x.id === c)?.name ?? `#${c}` }));
    return [];
  }, [step, basic, strategy, password, selected, roleIds, companies, t]);

  const create = async () => {
    setBusy(true);
    setError(null);
    const finalPassword = strategy === 'temporary' ? password : generatePassword();
    let created: { id: number; userName: string } | null = null;
    const failed: string[] = [];
    try {
      const user = await usersApi.create({
        userName: basic.userName.trim(), email: basic.email.trim(), firstName: basic.firstName.trim(), lastName: basic.lastName.trim(),
        phoneNumber: basic.phone.trim() || null, password: finalPassword, shouldChangePasswordOnNextLogin: true,
      });
      created = { id: user.id, userName: user.userName };
      for (const companyId of selected) {
        try {
          await usersApi.memberships.create({ userId: user.id, companyId, membershipType: UserType.Standard });
          for (const roleId of roleIds[companyId] ?? []) await usersApi.roleGrants.create({ userId: user.id, companyId, roleId });
        } catch (e) { failed.push(`${companies.find(c => c.id === companyId)?.name ?? companyId}: ${(e as Error).message}`); }
      }
      if (activeId !== null) {
        try { await usersApi.update(user.id, { userName: user.userName, email: user.email, firstName: user.firstName, lastName: user.lastName, phoneNumber: user.phoneNumber, activeCompanyId: activeId }); }
        catch (e) { failed.push(`${t('Active company')}: ${(e as Error).message}`); }
      }
      for (const unitId of unitIds) {
        try { await unitsApi.members.create({ userId: user.id, unitId, isPrimary: unitId === primaryUnit }); } catch (e) { failed.push(`${units.find(u => u.id === unitId)?.name ?? unitId}: ${(e as Error).message}`); }
      }
      toast.success(t('User saved'));
      setDone({ ...created, password: finalPassword, problems: failed });
    } catch (e) {
      setError((e as Error).message);
      toast.error((e as Error).message);
    } finally {
      setBusy(false);
    }
  };

  if (done) {
    return (
      <Modal title={t('User created')} onClose={() => navigate(`/admin/users/${done.id}`)} width={560} compact footer={<Button label={t('Open the user')} onClick={() => navigate(`/admin/users/${done.id}`)} />}>
        <div className="stack">
          <div className="alert alert-success">{t('{name} was created.', { name: done.userName })}</div>
          <div className="alert alert-warning">{t('Temporary password (shown once):')} <code>{done.password}</code><div className="muted-text">{t('They must change it at the first sign-in.')}</div></div>
          {done.problems.length > 0 && <div className="alert alert-error">{t('These could not be set; do them in the editor:')}<ul>{done.problems.map(p => <li key={p}>{p}</li>)}</ul></div>}
        </div>
      </Modal>
    );
  }

  const steps = [t('Basic'), t('Companies'), t('Roles'), t('Units'), t('Review')].map(label => ({ label }));
  const chosenCompanies = companies.filter(c => selected.includes(c.id));
  const companyOf = (u: UnitRead) => u.companyId === null || selected.includes(u.companyId);

  return (
    <>
      <PageHeader title={t('New user')} actions={<Button outlined severity="secondary" icon="pi pi-arrow-left" label={t('Back')} onClick={() => navigate('/admin/users')} />} />
      <div className="card card-pad" style={{ maxWidth: 900 }}>
        <Steps model={steps} activeIndex={step} readOnly />
        <div style={{ marginTop: 22, minHeight: 260 }}>
          {step === 0 && (
            <div className="stack">
              <div className="form-grid">
                <TextField label={t('First name')} required value={basic.firstName} onChange={e => set({ firstName: e.target.value })} />
                <TextField label={t('Last name')} required value={basic.lastName} onChange={e => set({ lastName: e.target.value })} />
                <TextField label={t('User name')} required value={basic.userName} onChange={e => set({ userName: e.target.value.toLowerCase() })} />
                <TextField label={t('E-mail')} type="email" required value={basic.email} onChange={e => set({ email: e.target.value.toLowerCase() })} />
                <TextField label={t('Phone')} value={basic.phone} onChange={e => set({ phone: e.target.value })} />
              </div>
              <Field label={t('First password')}>
                <div className="stack">
                  <label className="check"><RadioButton checked={strategy === 'generated'} onChange={() => setStrategy('generated')} /><span>{t('Generate one and show it once')}</span></label>
                  <label className="check"><RadioButton checked={strategy === 'temporary'} onChange={() => setStrategy('temporary')} /><span>{t('I choose a temporary password')}</span></label>
                  {strategy === 'temporary' && <TextField label="" type="password" value={password} onChange={e => setPassword(e.target.value)} />}
                </div>
              </Field>
            </div>
          )}

          {step === 1 && (
            <div className="stack">
              {companies.length === 0 && <EmptyState title={t('No companies yet')} />}
              <div className="perm-grid">
                {companies.map(c => (
                  <div key={c.id} className="card card-pad">
                    <CheckField label={c.name} hint={c.code} checked={selected.includes(c.id)} onChange={v => toggleCompany(c.id, v)} />
                    {selected.includes(c.id) && (
                      <Button size="small" outlined={activeId !== c.id} severity={activeId === c.id ? 'success' : 'secondary'} style={{ marginTop: 8 }}
                              label={activeId === c.id ? t('Active') : t('Active company')} onClick={() => setActiveId(c.id)} />
                    )}
                  </div>
                ))}
              </div>
            </div>
          )}

          {step === 2 && (
            <div className="stack">
              {chosenCompanies.map(c => (
                <div key={c.id} className="card card-pad">
                  <strong>{c.name}</strong>
                  <div className="row row-wrap" style={{ gap: 8, marginTop: 8 }}>
                    {roles.map(r => {
                      const on = roleIds[c.id]?.includes(r.id);
                      return <Button key={r.id} size="small" outlined={!on} severity={on ? 'success' : 'secondary'} label={`${on ? '✓' : '+'} ${r.name}`} onClick={() => toggleRole(c.id, r.id)} />;
                    })}
                  </div>
                </div>
              ))}
            </div>
          )}

          {step === 3 && (
            <div className="stack">
              <div className="alert alert-info">{t('Optional. You can change units later in the editor.')}</div>
              <Field label={t('Units')}>
                <MultiSelect value={unitIds} options={units.filter(companyOf).map(u => ({ label: `${u.name} (${u.code})`, value: u.id }))} display="chip" filter
                             onChange={e => { setUnitIds(e.value as number[]); if (primaryUnit !== null && !(e.value as number[]).includes(primaryUnit)) setPrimaryUnit(null); }} />
              </Field>
              {unitIds.length > 0 && (
                <SelectField label={t('Primary unit')} placeholder="—" value={primaryUnit} options={units.filter(u => unitIds.includes(u.id)).map(u => ({ value: u.id, label: u.name }))}
                             onChange={e => setPrimaryUnit(e.target.value === '' ? null : Number(e.target.value))} />
              )}
            </div>
          )}

          {step === 4 && (
            <dl className="kv">
              <dt>{t('Name')}</dt><dd>{basic.firstName} {basic.lastName}</dd>
              <dt>{t('User name')}</dt><dd>@{basic.userName}</dd>
              <dt>{t('E-mail')}</dt><dd>{basic.email}</dd>
              <dt>{t('Phone')}</dt><dd>{basic.phone || '—'}</dd>
              <dt>{t('Companies')}</dt><dd>{chosenCompanies.map(c => <Badge key={c.id} tone={c.id === activeId ? 'success' : 'neutral'}>{c.name}</Badge>)}</dd>
              <dt>{t('Roles')}</dt>
              <dd>{chosenCompanies.map(c => <div key={c.id}>{c.name}: {(roleIds[c.id] ?? []).map(id => roles.find(r => r.id === id)?.name).join(', ')}</div>)}</dd>
              <dt>{t('Units')}</dt><dd>{unitIds.length ? unitIds.map(id => <Badge key={id} tone={id === primaryUnit ? 'info' : 'neutral'}>{units.find(u => u.id === id)?.name}</Badge>) : '—'}</dd>
              <dt>{t('First password')}</dt><dd>{strategy === 'generated' ? t('Generated, shown once') : t('Temporary, chosen by you')}</dd>
            </dl>
          )}
        </div>

        {error && <div className="alert alert-error" role="alert" style={{ marginTop: 12 }}>{error}</div>}
        {problems.length > 0 && <div className="alert alert-warning" style={{ marginTop: 12 }}>{problems.map(p => <div key={p}>{p}</div>)}</div>}

        <div className="row" style={{ justifyContent: 'space-between', marginTop: 18 }}>
          <Button text severity="secondary" icon="pi pi-arrow-left" label={t('Back')} disabled={step === 0 || busy} onClick={() => setStep(s => s - 1)} />
          {step < 4
            ? <Button icon="pi pi-arrow-right" iconPos="right" label={t('Next')} disabled={problems.length > 0} onClick={() => setStep(s => s + 1)} />
            : <Button icon="pi pi-check" label={t('Create')} loading={busy} disabled={busy} onClick={() => void create()} />}
        </div>
      </div>
    </>
  );
}
