import { useCallback, useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { Button } from 'primereact/button';
import { TabPanel, TabView } from 'primereact/tabview';
import { Avatar } from '@/components/ui/RowMenu';
import { Badge, EmptyState, PageHeader, Spinner } from '@/components/ui/Form';
import { useToast } from '@/components/ui/Toast';
import { useAuth } from '@/features/auth/AuthContext';
import { P } from '@/features/auth/permissions';
import { rolesApi, type RoleRead } from '@/features/roles/roles.service';
import { companiesApi, type CompanyRead } from '@/features/tenants/organization.service';
import { usersApi, type UserCompanyRead, type UserRead } from '@/features/users/users.service';
import { useI18n } from '@/i18n';
import { UserCopyDialog } from './dialogs/UserCopyDialog';
import { BasicInfoTab } from './tabs/BasicInfoTab';
import { CompaniesTab, PermissionsTab, RolesTab, UnitsTab, UserTypeTab } from './tabs/AccessTabs';
import { ActivityTab, SecurityTab, SessionsTab } from './tabs/SecurityTabs';

export function UserEditorPage() {
  const { t, formatDate } = useI18n();
  const navigate = useNavigate();
  const toast = useToast();
  const { can } = useAuth();
  const userId = Number(useParams().id);

  const [user, setUser] = useState<UserRead | null>(null);
  const [companies, setCompanies] = useState<CompanyRead[]>([]);
  const [roles, setRoles] = useState<RoleRead[]>([]);
  const [memberships, setMemberships] = useState<UserCompanyRead[]>([]);
  const [loading, setLoading] = useState(true);
  const [tab, setTab] = useState(0);
  const [dirty, setDirty] = useState(false);
  const [copying, setCopying] = useState(false);

  const reload = useCallback(async () => {
    try {
      const [u, c, r, m] = await Promise.all([usersApi.get(userId), companiesApi.list(), rolesApi.list(), usersApi.memberships.list()]);
      setUser(u); setCompanies(c); setRoles(r); setMemberships(m.filter(x => x.userId === userId));
    } catch (e) {
      toast.error((e as Error).message);
      setUser(null);
    } finally {
      setLoading(false);
    }
  }, [userId, toast]);
  useEffect(() => { void reload(); }, [reload]);

  if (loading) return <Spinner label={t('Loading…')} />;
  if (!user) {
    return <div className="empty"><EmptyState title={t('User not found')} /><Button text icon="pi pi-arrow-left" label={t('Back to users')} onClick={() => navigate('/admin/users')} /></div>;
  }

  const name = `${user.firstName} ${user.lastName}`.trim() || user.userName;
  const canManage = can(P.UserManage);
  const active = companies.find(c => c.id === user.activeCompanyId);
  const common = { user, companies, memberships, onChanged: () => void reload() };
  const headers: [string, string][] = [
    ['pi pi-user', t('Basic')], ['pi pi-building', t('Companies')], ['pi pi-id-card', t('User type')], ['pi pi-shield', t('Roles')], ['pi pi-sitemap', t('Units')],
    ['pi pi-key', t('Permissions')], ['pi pi-lock', t('Security')], ['pi pi-desktop', t('Sessions')], ['pi pi-list', t('Activity')],
  ];

  return (
    <>
      <PageHeader
        title={name}
        actions={<>
          <Button outlined severity="secondary" icon="pi pi-arrow-left" label={t('Back')} onClick={() => navigate('/admin/users')} />
          {canManage && <Button text severity="secondary" icon="pi pi-copy" label={t('Copy')} onClick={() => setCopying(true)} />}
        </>}
      />

      <div className="card card-pad row row-wrap" style={{ marginBottom: 14, gap: 16 }}>
        <Avatar id={user.id} text={name} size={56} />
        <div className="grow">
          <div className="row row-wrap" style={{ gap: 8 }}>
            <h2>{name}</h2>
            {user.isActive ? <Badge tone="success">{t('Active')}</Badge> : <Badge>{t('Inactive')}</Badge>}
            {user.emailConfirmed ? <Badge tone="success">✓ {t('E-mail')}</Badge> : <Badge tone="warning">{t('E-mail pending')}</Badge>}
            {user.shouldChangePasswordOnNextLogin && <Badge tone="warning">{t('Must change the password')}</Badge>}
          </div>
          <div className="cell-sub mono">@{user.userName} · {user.email}{user.phoneNumber ? ` · ${user.phoneNumber}` : ''}</div>
          <div className="cell-sub">{t('Active company')}: {active?.name ?? '—'} · {t('Last modified')}: {formatDate(user.lastModifiedAt ?? user.createdAt)}</div>
        </div>
      </div>

      <div className="card">
        <TabView activeIndex={tab} onTabChange={e => { if (tab === 0 && dirty && !window.confirm(t('There are unsaved changes. Leave anyway?'))) return; setTab(e.index); }}>
          {headers.map(([icon, label], i) => (
            <TabPanel key={i} header={label} leftIcon={`${icon} mr-2`}>
              {tab === i && (
                <div style={{ padding: '4px 4px 8px' }}>
                  {i === 0 && <BasicInfoTab {...common} canEdit={canManage} onSaved={() => void reload()} onDirtyChange={setDirty} />}
                  {i === 1 && <CompaniesTab {...common} />}
                  {i === 2 && <UserTypeTab {...common} />}
                  {i === 3 && <RolesTab {...common} roles={roles} />}
                  {i === 4 && <UnitsTab user={user} onChanged={() => void reload()} />}
                  {i === 5 && <PermissionsTab {...common} />}
                  {i === 6 && <SecurityTab user={user} />}
                  {i === 7 && <SessionsTab user={user} />}
                  {i === 8 && <ActivityTab user={user} />}
                </div>
              )}
            </TabPanel>
          ))}
        </TabView>
      </div>

      {copying && <UserCopyDialog source={user} onClose={() => setCopying(false)} />}
    </>
  );
}
