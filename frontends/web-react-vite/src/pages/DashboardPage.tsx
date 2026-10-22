import { useCallback, useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Button } from 'primereact/button';
import { useAuth } from '@/features/auth/AuthContext';
import { P } from '@/features/auth/permissions';
import { dashboardApi, type DashboardView } from '@/features/dashboard/dashboard.service';
import { WidgetGrid } from '@/features/dashboard/widgets';
import { useI18n } from '@/i18n';
import { EmptyState, PageHeader, Spinner } from '@/components/ui/Form';

export function DashboardPage() {
  const { t, language } = useI18n();
  const { user, can } = useAuth();
  const navigate = useNavigate();
  const sessionKey = `${user?.id ?? ''}:${user?.activeCompanyId ?? ''}:${user?.activeApplicationTypeId ?? ''}`;

  const [state, setState] = useState<{ key: string; view: DashboardView | null; failed: boolean }>({ key: '', view: null, failed: false });
  const [round, setRound] = useState(0);
  const seq = useRef(0);
  const current = state.key === sessionKey ? state : null;

  const load = useCallback((key: string) => {
    const mine = ++seq.current;
    setState({ key: '', view: null, failed: false });
    dashboardApi.mine()
      .then(view => { if (mine === seq.current) setState({ key, view, failed: false }); })
      .catch(() => { if (mine === seq.current) setState({ key, view: null, failed: true }); });
  }, []);
  useEffect(() => { load(sessionKey); }, [load, sessionKey]);

  const company = user?.companies.find(c => c.companyId === user.activeCompanyId);
  const today = new Intl.DateTimeFormat(language === 'en' ? 'en-GB' : language, { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' }).format(new Date());
  const name = user ? `${user.firstName} ${user.lastName}`.trim() || user.userName : '';

  return (
    <>
      <PageHeader title={t('Welcome, {name}', { name })} subtitle={[company?.name, today].filter(Boolean).join(' · ')}
                  actions={<>
                    <Button text severity="secondary" icon="pi pi-refresh" aria-label={t('Refresh')} onClick={() => { setRound(r => r + 1); load(sessionKey); }} />
                    {can(P.DashboardManage) && (
                      <Button outlined size="small" severity="secondary" icon="pi pi-objects-column" label={t('Customize')}
                              onClick={() => navigate(`/admin/dashboard-manager?scope=2${user?.activeCompanyId ? `&companyId=${user.activeCompanyId}` : ''}`)} />
                    )}
                  </>} />
      {!current ? <Spinner label={t('Loading…')} />
        : current.failed ? (
          <div className="card card-pad"><div className="empty">
            <i className="pi pi-exclamation-triangle" style={{ fontSize: 26, color: '#D97706' }} />
            <span>{t('The dashboard could not be loaded.')}</span>
            <Button size="small" outlined icon="pi pi-refresh" label={t('Try again')} onClick={() => load(sessionKey)} />
          </div></div>
        ) : !current.view || current.view.items.length === 0
          ? <EmptyState title={t('Your dashboard is empty')} text={t('Nothing here is available to you yet.')} />
          : <WidgetGrid key={`${sessionKey}:${round}`} items={current.view.items} />}
    </>
  );
}
