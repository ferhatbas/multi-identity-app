import { useEffect, useMemo, useState } from 'react';
import { TabPanel, TabView } from 'primereact/tabview';
import { ResourcePage, type FieldDef } from '@/components/ui/ResourcePage';
import type { Column } from '@/components/ui/DataTable';
import { Badge, PageHeader, Spinner } from '@/components/ui/Form';
import { useToast } from '@/components/ui/Toast';
import { P } from '@/features/auth/permissions';
import { rolesApi } from '@/features/roles/roles.service';
import { useOptions } from '@/features/lookups/useOptions';
import { securityApi, sodApi, type EffectivePolicy, type SecurityPolicyRead, type SoDRuleRead } from '@/features/security/security.service';
import { UserTypeNames } from '@/types/api';
import { useI18n } from '@/i18n';

function EffectivePolicyCard() {
  const { t } = useI18n();
  const toast = useToast();
  const [policy, setPolicy] = useState<EffectivePolicy | null>(null);

  useEffect(() => { securityApi.effective().then(setPolicy).catch((e: Error) => toast.error(e.message)); }, [toast]);
  if (!policy) return <Spinner label={t('Loading…')} />;

  const yes = (v: boolean) => (v ? <Badge tone="success">{t('Yes')}</Badge> : <Badge>{t('No')}</Badge>);
  return (
    <div className="card card-pad" style={{ marginBottom: 22 }}>
      <div className="card-title"><h2>{t('In force for you')}</h2></div>
      <dl className="kv" style={{ gridTemplateColumns: '260px 1fr' }}>
        <dt>{t('Minimum password length')}</dt><dd>{policy.minPasswordLength}</dd>
        <dt>{t('Requires a letter / digit / symbol')}</dt><dd>{yes(policy.passwordRequiresLetter)} {yes(policy.passwordRequiresDigit)} {yes(policy.passwordRequiresSymbol)}</dd>
        <dt>{t('Requires upper and lower case')}</dt><dd>{yes(policy.passwordRequiresMixedCase)}</dd>
        <dt>{t('Password valid for (days)')}</dt><dd>{policy.passwordMaxAgeDays || t('Unlimited')}</dd>
        <dt>{t('Lock after failed attempts')}</dt><dd>{policy.maxFailedAttempts} / {policy.lockoutWindowMinutes} min → {policy.lockoutMinutes} min</dd>
        <dt>{t('Session length (days)')}</dt><dd>{policy.sessionDays}</dd>
        <dt>{t('Two-factor required from rank')}</dt><dd>{policy.requireMfaFromRank === null ? t('Nobody') : UserTypeNames[policy.requireMfaFromRank]}</dd>
        <dt>{t('Confirmed e-mail required')}</dt><dd>{yes(policy.requireConfirmedEmail)}</dd>
      </dl>
    </div>
  );
}

export function SecurityPage() {
  const { t, formatDate } = useI18n();

  const columns = useMemo<Column<SecurityPolicyRead>[]>(() => [
    { key: 'name', header: t('Name'), render: r => <span className="cell-main">{r.name}</span>, csv: r => r.name },
    { key: 'scope', header: t('Scope'), render: r => (r.tenantId === null ? <Badge tone="info">{t('Platform')}</Badge> : <Badge>{t('Tenant')}</Badge>), csv: r => (r.tenantId === null ? 'platform' : 'tenant') },
    { key: 'len', header: t('Min. length'), render: r => r.minPasswordLength ?? '—', csv: r => r.minPasswordLength },
    { key: 'lock', header: t('Lockout'), render: r => (r.maxFailedAttempts ? `${r.maxFailedAttempts} → ${r.lockoutMinutes ?? '—'} min` : '—') },
    { key: 'mfa', header: '2FA', render: r => (r.requireMfaFromRank === null || r.requireMfaFromRank === undefined ? '—' : `≥ ${UserTypeNames[r.requireMfaFromRank]}`) },
    { key: 'created', header: t('Created'), render: r => formatDate(r.createdAt) },
  ], [t, formatDate]);

  const num = (name: keyof EffectivePolicy, label: string, hint?: string): FieldDef => ({ name, label, type: 'number', hint });
  const fields: FieldDef[] = [
    { name: 'name', label: t('Name'), required: true, maxLength: 128 },
    num('minPasswordLength', t('Minimum password length')),
    num('passwordMaxAgeDays', t('Password valid for (days)'), t('0 means unlimited.')),
    num('maxFailedAttempts', t('Failed attempts before lockout')),
    num('lockoutWindowMinutes', t('Window for counting attempts (min)')),
    num('lockoutMinutes', t('Lockout length (min)')),
    num('sessionDays', t('Session length (days)')),
    {
      name: 'requireMfaFromRank', label: t('Two-factor required from rank'), type: 'select',
      options: UserTypeNames.map((n, i) => ({ value: i, label: n })),
    },
    { name: 'passwordRequiresLetter', label: t('Requires a letter'), type: 'checkbox' },
    { name: 'passwordRequiresDigit', label: t('Requires a digit'), type: 'checkbox' },
    { name: 'passwordRequiresSymbol', label: t('Requires a symbol'), type: 'checkbox' },
    { name: 'passwordRequiresMixedCase', label: t('Requires upper and lower case'), type: 'checkbox' },
    { name: 'requireConfirmedEmail', label: t('Confirmed e-mail required'), type: 'checkbox' },
  ];

  const perms = useOptions(() => rolesApi.permissions.list(), r => ({ value: r.id, label: r.code }));
  const permName = (id: number) => perms.options.find(o => o.value === id)?.label ?? `#${id}`;

  const sodColumns = useMemo<Column<SoDRuleRead>[]>(() => [
    { key: 'name', header: t('Name'), sortKey: 'name', render: r => <span className="cell-main">{r.name}</span>, csv: r => r.name },
    { key: 'a', header: t('Permission A'), sortKey: 'permissionAId', render: r => <code>{permName(r.permissionAId)}</code>, csv: r => permName(r.permissionAId) },
    { key: 'b', header: t('Permission B'), sortKey: 'permissionBId', render: r => <code>{permName(r.permissionBId)}</code>, csv: r => permName(r.permissionBId) },
    { key: 'description', header: t('Description'), render: r => r.description || '—', csv: r => r.description },
    { key: 'scope', header: t('Scope'), render: r => (r.tenantId === null ? <Badge tone="info">{t('Platform')}</Badge> : <Badge>{t('Tenant')}</Badge>), csv: r => (r.tenantId === null ? 'platform' : 'tenant') },
  // eslint-disable-next-line react-hooks/exhaustive-deps
  ], [t, perms.options]);

  const sodFields: FieldDef[] = [
    { name: 'name', label: t('Name'), required: true, maxLength: 256 },
    { name: 'permissionAId', label: t('Permission A'), type: 'select', required: true, options: perms.options },
    { name: 'permissionBId', label: t('Permission B'), type: 'select', required: true, options: perms.options },
    { name: 'description', label: t('Description'), type: 'textarea', wide: true, maxLength: 512 },
    { name: 'isActive', label: t('Active'), type: 'checkbox' },
  ];

  return (
    <>
      <PageHeader title={t('Security')} subtitle={t('Security policies and segregation-of-duties rules')} />
      <EffectivePolicyCard />
      <div className="card">
        <TabView>
          <TabPanel header={t('Policies')} leftIcon="pi pi-shield mr-2">
            <p className="muted-text">{t('Password, lockout, session and two-factor rules. A tenant can only tighten the platform’s.')}</p>
            <ResourcePage<SecurityPolicyRead>
              title={t('Policies')} singular={t('Policy')} api={securityApi.policies} columns={columns} fields={fields}
              writePermission={P.SecurityManage} describeRow={r => r.name} exportName="security-policies"
            />
          </TabPanel>
          <TabPanel header={t('SoD rules')} leftIcon="pi pi-ban mr-2">
            <p className="muted-text">{t('Permission pairs that must not coexist on the same user in a company, however they are obtained.')}</p>
            <ResourcePage<SoDRuleRead>
              title={t('SoD rules')} singular={t('Rule')} api={sodApi} columns={sodColumns} fields={sodFields}
              writePermission={P.SecurityManage} blank={{ isActive: true }} describeRow={r => r.name} exportName="sod-rules"
            />
          </TabPanel>
        </TabView>
      </div>
    </>
  );
}
