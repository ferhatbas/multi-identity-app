import { useEffect, useRef, useState } from 'react';
import { NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom';
import { Button } from 'primereact/button';
import { Menu } from 'primereact/menu';
import type { MenuItem } from 'primereact/menuitem';
import { useAuth } from '@/features/auth/AuthContext';
import { authApi } from '@/features/auth/auth.service';
import { navigationApi, type MenuNode } from '@/features/navigation/navigation.service';
import { LANGUAGES, useI18n } from '@/i18n';
import { Icon } from '@/components/ui/Icon';
import { useToast } from '@/components/ui/Toast';
import { getAccessToken } from '@/features/auth/session';
import { companiesApi, tenantsApi } from '@/features/tenants/organization.service';
import { useTheme } from './ThemeContext';

function useMenu(): MenuNode[] {
  const { user } = useAuth();
  const [menu, setMenu] = useState<MenuNode[]>([]);
  const scope = `${user?.id}:${user?.activeCompanyId}:${user?.activeApplicationTypeId}`;

  useEffect(() => {
    let live = true;
    navigationApi.mine().then(m => { if (live) setMenu(m); }).catch(() => { if (live) setMenu([]); });
    return () => { live = false; };
  }, [scope]);

  return menu;
}

function NavItem({ node, depth = 0 }: { node: MenuNode; depth?: number }) {
  if (node.children.length > 0 && !node.path) {
    return (
      <>
        <div className="nav-group">{node.title}</div>
        {node.children.map(c => <NavItem key={c.id} node={c} depth={depth + 1} />)}
      </>
    );
  }
  return (
    <NavLink to={node.path ?? '#'} className={({ isActive }) => `nav-link${depth > 1 ? ' nav-child' : ''}${isActive ? ' active' : ''}`}>
      <Icon name={node.icon} size={15} />
      <span>{node.title}</span>
    </NavLink>
  );
}

function MenuButton({ items, children, label }: { items: MenuItem[]; children: React.ReactNode; label: string }) {
  const menu = useRef<Menu>(null);
  return (
    <>
      <Menu model={items} popup ref={menu} popupAlignment="right" />
      <Button text severity="secondary" size="small" aria-label={label} aria-haspopup onClick={e => menu.current?.toggle(e)}
              style={{ padding: '0.3rem 0.55rem', gap: 6 }}>
        {children}
      </Button>
    </>
  );
}

interface SwitchableCompany { id: number; name: string; tenant: string | null }

function useSwitchableCompanies(): SwitchableCompany[] {
  const { user } = useAuth();
  const [all, setAll] = useState<SwitchableCompany[] | null>(null);
  const platform = !!user?.isPlatformAdmin;

  useEffect(() => {
    if (!platform) return;
    let live = true;
    Promise.all([companiesApi.list(), tenantsApi.list()])
      .then(([companies, tenants]) => {
        const names = new Map(tenants.map(t => [t.id, t.name]));
        const list = companies.filter(c => c.isActive)
          .map(c => ({ id: c.id, name: c.name, tenant: c.tenantId ? names.get(c.tenantId) ?? `#${c.tenantId}` : null }))
          .sort((x, y) => (x.tenant ?? '').localeCompare(y.tenant ?? '') || x.name.localeCompare(y.name));
        if (live) setAll(list);
      })
      .catch(() => { if (live) setAll(null); });
    return () => { live = false; };
  }, [platform]);

  if (platform && all) return all;
  return (user?.companies ?? []).map(c => ({ id: c.companyId, name: c.name, tenant: null }));
}

function CompanySwitcher() {
  const { user, switchCompany } = useAuth();
  const { t } = useI18n();
  const toast = useToast();
  const navigate = useNavigate();
  const companies = useSwitchableCompanies();

  if (!user || companies.length === 0) return null;
  const current = companies.find(c => c.id === user.activeCompanyId);

  const choose = async (companyId: number) => {
    if (companyId === user.activeCompanyId) return;
    try {
      await switchCompany(companyId);
      navigate('/');
    } catch (e) {
      toast.error((e as Error).message);
    }
  };

  const entry = (c: SwitchableCompany): MenuItem => ({
    label: c.name,
    icon: c.id === user.activeCompanyId ? 'pi pi-check' : 'pi pi-building',
    className: c.id === user.activeCompanyId ? 'is-current' : undefined,
    command: () => void choose(c.id),
  });

  const tenants = [...new Set(companies.map(c => c.tenant))];
  const items: MenuItem[] = tenants.length > 1 || tenants[0] !== null
    ? tenants.map(tenant => ({ label: tenant ?? t('Company'), items: companies.filter(c => c.tenant === tenant).map(entry) }))
    : [{ label: t('Company'), items: companies.map(entry) }];

  return (
    <MenuButton items={items} label={t('Company')}>
      <span className="dot-ok" />
      <span className="hide-sm">{current?.name ?? t('Choose a company')}</span>
      <i className="pi pi-chevron-down" style={{ fontSize: 10 }} />
    </MenuButton>
  );
}

function LanguageSwitcher() {
  const { t, language, setLanguage } = useI18n();
  const { user, reload } = useAuth();
  const toast = useToast();

  const choose = async (code: string | null) => {
    if (!user) { setLanguage(code); return; }
    try {
      const mine = await authApi.setLanguage(code);
      setLanguage(mine.effectiveLanguageCode);
      await reload();
    } catch (e) { toast.error((e as Error).message); }
  };

  const items: MenuItem[] = LANGUAGES.map(l => ({
    label: l.label,
    icon: l.code === language ? 'pi pi-check' : 'pi pi-globe',
    className: l.code === language ? 'is-current' : undefined,
    command: () => void choose(l.code),
  }));
  if (user) {
    items.push({ separator: true });
    items.push(user.languageCode
      ? { label: t('Use the organisation language'), icon: 'pi pi-replay', command: () => void choose(null) }
      : { label: t('Using the organisation default'), icon: 'pi pi-building', disabled: true });
  }
  return (
    <MenuButton items={items} label={t('Language')}>
      <span style={{ fontWeight: 600 }}>{language.toUpperCase()}</span>
      <i className="pi pi-chevron-down" style={{ fontSize: 10 }} />
    </MenuButton>
  );
}

function UserMenu() {
  const { user, signOut } = useAuth();
  const { t } = useI18n();
  const navigate = useNavigate();
  if (!user) return null;

  const initials = `${user.firstName[0] ?? ''}${user.lastName[0] ?? ''}`.toUpperCase() || user.userName[0]?.toUpperCase();
  const items: MenuItem[] = [
    { label: user.userName, items: [
      { label: t('My account'), icon: 'pi pi-user', command: () => navigate('/account') },
      { separator: true },
      { label: t('Sign out'), icon: 'pi pi-sign-out', command: () => void signOut().then(() => navigate('/auth/login')) },
    ] },
  ];

  return (
    <MenuButton items={items} label={user.userName}>
      <span className="avatar">{initials}</span>
      <span className="hide-sm" style={{ textAlign: 'left', lineHeight: 1.2 }}>
        <span style={{ display: 'block', fontWeight: 600, color: 'var(--text)' }}>{user.firstName} {user.lastName}</span>
        <small style={{ color: 'var(--text-3)', fontWeight: 400 }}>{user.isPlatformAdmin ? t('Platform administrator') : t('Member')}</small>
      </span>
    </MenuButton>
  );
}

function isImpersonating(): boolean {
  try {
    const payload = getAccessToken()?.split('.')[1]?.replace(/-/g, '+').replace(/_/g, '/');
    return !!payload && JSON.parse(atob(payload)).ImpersonatorId !== undefined;
  } catch {
    return false;
  }
}

export function MainLayout() {
  const menu = useMenu();
  const { user, signOut } = useAuth();
  const navigate = useNavigate();
  const { t } = useI18n();
  const { theme, toggle } = useTheme();
  const [open, setOpen] = useState(false);
  const location = useLocation();

  useEffect(() => setOpen(false), [location.pathname]);

  return (
    <div className="app">
      {isImpersonating() && (
        <div className="alert alert-warning" style={{ borderRadius: 0, display: 'flex', gap: 12, alignItems: 'center' }}>
          <Icon name="eye" size={16} />
          <span className="grow">{t('You are acting as {name}. Everything you do is recorded.', { name: user?.userName ?? '' })}</span>
          <Button size="small" outlined severity="secondary" label={t('Stop')} onClick={() => void signOut().then(() => navigate('/auth/login'))} />
        </div>
      )}
      <header className="app-header">
        <Button className="menu-toggle" text severity="secondary" icon="pi pi-bars" aria-label={t('Open menu')} onClick={() => setOpen(true)} />
        <NavLink to="/" className="brand">
          <span className="brand-mark"><Icon name="lock" size={15} /></span>
          FSIdentity
        </NavLink>
        <div className="head-right">
          <CompanySwitcher />
          <LanguageSwitcher />
          <Button text severity="secondary" icon={theme === 'dark' ? 'pi pi-sun' : 'pi pi-moon'} aria-label={t('Switch theme')} tooltip={t('Switch theme')}
                  tooltipOptions={{ position: 'bottom' }} onClick={toggle} />
          <span className="head-sep" />
          <UserMenu />
        </div>
      </header>

      <div className="app-body">
        {open && <div className="scrim" onClick={() => setOpen(false)} />}
        <aside className={`sidebar${open ? ' open' : ''}`}>
          <nav className="nav" aria-label={t('Main menu')}>
            {menu.map(n => <NavItem key={n.id} node={n} />)}
            <div className="nav-group">{t('Account')}</div>
            <NavLink to="/account" className={({ isActive }) => `nav-link${isActive ? ' active' : ''}`}>
              <Icon name="user" size={15} /><span>{t('My account')}</span>
            </NavLink>
          </nav>
          <div className="side-foot"><span className="dot-ok" />v0.1 · {t('Identity console')}</div>
        </aside>
        <div className="main-col">
          <main className="content"><Outlet /></main>
        </div>
      </div>
    </div>
  );
}
