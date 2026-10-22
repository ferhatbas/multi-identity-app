import { Component, useEffect, useMemo, useState, type ErrorInfo, type ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { Column as PrimeColumn } from 'primereact/column';
import { DataTable as PrimeTable } from 'primereact/datatable';
import { Badge, EmptyState, Spinner } from '@/components/ui/Form';
import { auditApi, type AuditLogRead } from '@/features/audit/audit.service';
import { useAuth } from '@/features/auth/AuthContext';
import { P } from '@/features/auth/permissions';
import { rolesApi } from '@/features/roles/roles.service';
import { companiesApi, tenantsApi } from '@/features/tenants/organization.service';
import { usersApi } from '@/features/users/users.service';
import { useI18n } from '@/i18n';
import type { DashboardItem } from './dashboard.service';

function Welcome() {
  const { user } = useAuth();
  const { t } = useI18n();
  if (!user) return null;
  const company = user.companies.find(c => c.companyId === user.activeCompanyId);
  return (
    <div className="card widget hero">
      <span className="avatar" style={{ width: 40, height: 40, fontSize: 14 }}>{`${user.firstName[0] ?? ''}${user.lastName[0] ?? ''}`.toUpperCase()}</span>
      <div>
        <h2 style={{ fontSize: 18 }}>{t('Hello, {name}', { name: user.firstName })}</h2>
        <p>{company ? t('You are working in {company}.', { company: company.name }) : t('Choose a company to start.')}</p>
      </div>
    </div>
  );
}

function KpiStrip() {
  const { can } = useAuth();
  const { t } = useI18n();
  const [counts, setCounts] = useState<Record<string, number | null>>({});

  useEffect(() => {
    const one = (key: string, allowed: boolean, load: () => Promise<{ totalRecords: number }>) =>
      allowed ? load().then(p => [key, p.totalRecords] as const).catch(() => [key, null] as const) : Promise.resolve(null);
    void Promise.all([
      one('users', can(P.UserView), () => usersApi.paged({ pageSize: 1 })),
      one('roles', can(P.RoleView), () => rolesApi.paged({ pageSize: 1 })),
      one('companies', can(P.CompanyManage) || can(P.TenantView), () => companiesApi.paged({ pageSize: 1 })),
      one('tenants', can(P.TenantView) || can(P.TenantManage), () => tenantsApi.paged({ pageSize: 1 })),
    ]).then(pairs => setCounts(Object.fromEntries(pairs.filter((x): x is readonly [string, number | null] => x !== null))));
  }, [can]);

  const items: [string, string][] = [['users', t('Users')], ['roles', t('Roles')], ['companies', t('Companies')], ['tenants', t('Tenants')]];
  const shown = items.filter(([key]) => key in counts);
  if (shown.length === 0) return null;
  return (
    <div className="kpis">
      {shown.map(([key, label]) => (
        <div key={key} className="kpi">
          <span className="kpi-label">{label}</span>
          <span className="kpi-value">{counts[key] ?? '—'}</span>
        </div>
      ))}
    </div>
  );
}

function QuickActions() {
  const { can } = useAuth();
  const { t } = useI18n();
  const links: { to: string; icon: string; label: string; allowed: boolean }[] = [
    { to: '/admin/users/new', icon: 'pi-user-plus', label: t('New user'), allowed: can(P.UserManage) },
    { to: '/admin/users', icon: 'pi-users', label: t('Users'), allowed: can(P.UserView) },
    { to: '/admin/roles', icon: 'pi-shield', label: t('Roles'), allowed: can(P.RoleView) },
    { to: '/admin/companies', icon: 'pi-building', label: t('Companies'), allowed: can(P.CompanyManage) || can(P.TenantView) },
    { to: '/admin/units', icon: 'pi-sitemap', label: t('Units'), allowed: can(P.UnitManage) },
    { to: '/admin/audit', icon: 'pi-history', label: t('Audit trail'), allowed: can(P.AuditLogView) },
  ].filter(l => l.allowed);
  if (links.length === 0) return null;
  return (
    <div className="card widget">
      <h3>{t('Quick actions')}</h3>
      <div className="row row-wrap" style={{ marginTop: 10, gap: 8 }}>
        {links.map(l => (
          <Link key={l.to} to={l.to} className="p-button p-button-outlined p-button-secondary p-button-sm">
            <i className={`pi ${l.icon}`} style={{ marginRight: 6 }} />{l.label}
          </Link>
        ))}
      </div>
    </div>
  );
}

const DAY = 24 * 60 * 60 * 1000;
const day = (d: Date) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;

function AuditActions() {
  const { t } = useI18n();
  const [rows, setRows] = useState<AuditLogRead[] | null>(null);

  useEffect(() => {
    auditApi.paged({ pageSize: 500, from: day(new Date(Date.now() - 6 * DAY)) }).then(p => setRows(p.data)).catch(() => setRows([]));
  }, []);

  const bars = useMemo(() => {
    const counts = new Map<string, number>();
    for (const r of rows ?? []) counts.set(r.action, (counts.get(r.action) ?? 0) + 1);
    return [...counts.entries()].sort((a, b) => b[1] - a[1]);
  }, [rows]);
  const max = Math.max(1, ...bars.map(b => b[1]));

  return (
    <div className="card widget">
      <h3>{t('Changes in the last 7 days')}</h3>
      {!rows ? <Spinner /> : bars.length === 0 ? <p className="muted-text">{t('No changes recorded')}</p> : (
        <div className="stack" style={{ marginTop: 10 }}>
          {bars.map(([action, count]) => (
            <div key={action} className="row">
              <span style={{ width: 80 }}>{action}</span>
              <div style={{ flex: 1, background: 'var(--border)', borderRadius: 4, height: 10 }}>
                <div style={{ width: `${(count / max) * 100}%`, background: 'var(--primary)', height: '100%', borderRadius: 4 }} />
              </div>
              <span className="mono muted-text" style={{ width: 36, textAlign: 'right' }}>{count}</span>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

function RecentActivity() {
  const { t, formatDate } = useI18n();
  const [rows, setRows] = useState<AuditLogRead[] | null>(null);

  useEffect(() => { auditApi.paged({ pageSize: 8 }).then(p => setRows(p.data)).catch(() => setRows([])); }, []);

  return (
    <div className="card table-card">
      <div className="card-title" style={{ padding: '12px 16px 0' }}><h3>{t('Recent activity')}</h3></div>
      {!rows ? <Spinner /> : rows.length === 0 ? <EmptyState title={t('No changes recorded')} /> : (
        <PrimeTable value={rows} size="small">
          <PrimeColumn header={t('Action')} body={(r: AuditLogRead) => <Badge>{r.action}</Badge>} />
          <PrimeColumn header={t('Record')} body={(r: AuditLogRead) => <><span className="cell-main">{r.entityName}</span> <span className="muted-text">#{r.entityId}</span></>} />
          <PrimeColumn header={t('Who')} body={(r: AuditLogRead) => r.userName ?? '—'} />
          <PrimeColumn header={t('When')} body={(r: AuditLogRead) => <span className="mono muted-text">{formatDate(r.createdAt)}</span>} />
        </PrimeTable>
      )}
    </div>
  );
}

const REGISTRY: Record<string, () => ReactNode> = {
  WELCOME: () => <Welcome />,
  ADMIN_KPI_STRIP: () => <KpiStrip />,
  ADMIN_QUICK_ACTIONS: () => <QuickActions />,
  ADMIN_AUDIT_ACTIONS: () => <AuditActions />,
  RECENT_ACTIVITY: () => <RecentActivity />,
};

class WidgetBoundary extends Component<{ code: string; children: ReactNode }, { failed: boolean }> {
  state = { failed: false };
  static getDerivedStateFromError() { return { failed: true }; }
  componentDidCatch(error: Error, info: ErrorInfo) { console.error(`Dashboard widget ${this.props.code} failed`, error, info.componentStack); }
  render() { return this.state.failed ? <WidgetFailed /> : this.props.children; }
}

function WidgetFailed() {
  const { t } = useI18n();
  return (
    <div className="card card-pad" style={{ height: '100%' }}>
      <div className="empty" style={{ padding: '18px 0' }}>
        <i className="pi pi-exclamation-triangle" style={{ fontSize: 22, color: '#D97706' }} />
        <span>{t('This panel could not be displayed.')}</span>
      </div>
    </div>
  );
}

export function WidgetGrid({ items }: { items: DashboardItem[] }) {
  return (
    <div className="grid-24">
      {items.map(item => {
        const draw = REGISTRY[item.widget];
        return (
          <div key={item.widget} className="widget-slot" data-widget={item.widget}
               style={{ gridColumn: `${item.x + 1} / span ${item.w}`, gridRow: `${item.y + 1} / span ${Math.max(1, Math.ceil(item.h / 3))}` }}>
            <WidgetBoundary code={item.widget}>
              {draw ? draw() : <div className="card widget"><h3>{item.title}</h3></div>}
            </WidgetBoundary>
          </div>
        );
      })}
    </div>
  );
}
