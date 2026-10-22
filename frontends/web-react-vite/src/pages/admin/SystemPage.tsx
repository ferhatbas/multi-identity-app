import { useEffect, useMemo, useState } from 'react';
import { TabPanel, TabView } from 'primereact/tabview';
import { PageHeader, Spinner } from '@/components/ui/Form';
import { useToast } from '@/components/ui/Toast';
import { getAccessToken } from '@/features/auth/session';
import { rolesApi } from '@/features/roles/roles.service';
import { companiesApi, tenantsApi } from '@/features/tenants/organization.service';
import { usersApi } from '@/features/users/users.service';
import { useI18n } from '@/i18n';

function claims(): Record<string, unknown> | null {
  const token = getAccessToken();
  if (!token) return null;
  try {
    const body = token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
    return JSON.parse(decodeURIComponent(escape(atob(body)))) as Record<string, unknown>;
  } catch { return null; }
}

interface Counts { tenants: number; companies: number; users: number; activeUsers: number; roles: number; permissions: number }

function Facts({ rows }: { rows: [string, string][] }) {
  return <dl className="kv" style={{ maxWidth: 560 }}>{rows.map(([k, v]) => <><dt key={`k${k}`}>{k}</dt><dd key={`v${k}`} className="mono">{v}</dd></>)}</dl>;
}

export function SystemPage() {
  const { t } = useI18n();
  const toast = useToast();
  const [counts, setCounts] = useState<Counts | null>(null);
  const jwt = useMemo(claims, []);

  useEffect(() => {
    Promise.all([tenantsApi.paged({ pageSize: 1 }), companiesApi.paged({ pageSize: 1 }), usersApi.list(), rolesApi.paged({ pageSize: 1 }), rolesApi.permissions.paged({ pageSize: 1 })])
      .then(([tn, co, users, ro, pe]) => setCounts({
        tenants: tn.totalRecords, companies: co.totalRecords, users: users.length, activeUsers: users.filter(u => u.isActive).length, roles: ro.totalRecords, permissions: pe.totalRecords,
      })).catch(e => toast.error((e as Error).message));
  }, []); // eslint-disable-line react-hooks/exhaustive-deps

  const hours = typeof jwt?.exp === 'number' && typeof jwt?.iat === 'number' ? String(Math.round(((jwt.exp - jwt.iat) / 3600) * 10) / 10) : '—';
  const kpis: [string, number][] = counts ? [
    [t('Tenants'), counts.tenants], [t('Companies'), counts.companies], [t('Users'), counts.users],
    [t('Active users'), counts.activeUsers], [t('Roles'), counts.roles], [t('Permissions'), counts.permissions],
  ] : [];

  return (
    <>
      <PageHeader title={t('System')} subtitle={t('A read-only snapshot of the platform.')} />
      <div className="card">
        <TabView>
          <TabPanel header={t('Token')} leftIcon="pi pi-lock mr-2">
            <p className="muted-text">{t('The signing key and lifetimes are set in the server’s configuration, not here. This is what your own token carries.')}</p>
            <Facts rows={[[t('Issuer'), String(jwt?.iss ?? '—')], [t('Audience'), String(jwt?.aud ?? '—')], [t('Lifetime (hours)'), hours]]} />
          </TabPanel>
          <TabPanel header={t('Overview')} leftIcon="pi pi-chart-bar mr-2">
            {!counts ? <Spinner label={t('Loading…')} /> : (
              <div className="kpis">
                {kpis.map(([label, value]) => <div key={label} className="kpi"><span className="kpi-label">{label}</span><span className="kpi-value">{value}</span></div>)}
              </div>
            )}
          </TabPanel>
          <TabPanel header={t('Version')} leftIcon="pi pi-info-circle mr-2">
            <Facts rows={[[t('Product'), 'FSIdentity'], [t('Stack'), '.NET 10 · React 19 · Vite · SQL Server 2022'], [t('Console mode'), import.meta.env.MODE]]} />
          </TabPanel>
        </TabView>
      </div>
    </>
  );
}
