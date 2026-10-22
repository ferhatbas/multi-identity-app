import type { ReactNode } from 'react';
import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { useT } from '@/i18n';
import { Spinner } from '@/components/ui/Form';
import { useAuth } from './AuthContext';

export function ProtectedRoute() {
  const { loading, user, restricted } = useAuth();
  const location = useLocation();
  const t = useT();

  if (loading) return <div className="boot"><Spinner label={t('Loading…')} /></div>;
  if (!user) return <Navigate to="/auth/login" replace state={{ from: location.pathname }} />;
  if (restricted && location.pathname !== '/required') return <Navigate to="/required" replace />;
  if (!restricted && location.pathname === '/required') return <Navigate to="/" replace />;
  return <Outlet />;
}

export function AccessDenied() {
  const t = useT();
  return (
    <div className="empty card card-pad">
      <strong>{t('You do not have access to this page')}</strong>
      <p>{t('Ask an administrator if you need it.')}</p>
    </div>
  );
}

export function RequirePermission({ permission, children }: { permission: string | string[]; children: ReactNode }) {
  const { can } = useAuth();
  const needed = Array.isArray(permission) ? permission : [permission];
  return needed.some(p => can(p)) ? <>{children}</> : <AccessDenied />;
}

export function RequirePlatformAdmin({ children }: { children: ReactNode }) {
  const { user } = useAuth();
  return user?.isPlatformAdmin ? <>{children}</> : <AccessDenied />;
}
