import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import type { AuthResponse, UserSummary } from '@/types/auth';
import { useI18n } from '@/i18n';
import { onSignedOut } from '@/services/http/httpService';
import { authApi } from './auth.service';
import { clearTokens, getAccessToken, setTokens } from './session';

export type SignInResult =
  | { kind: 'signed-in' }
  | { kind: 'two-factor'; twoFactorToken: string };

interface AuthState {
  loading: boolean;
  user: UserSummary | null;
  permissions: ReadonlySet<string>;
  modules: ReadonlySet<string>;
  restricted: boolean;
  signIn: (userName: string, password: string) => Promise<SignInResult>;
  completeTwoFactor: (twoFactorToken: string, code: string) => Promise<void>;
  signOut: () => Promise<void>;
  reload: () => Promise<void>;
  switchCompany: (companyId: number) => Promise<void>;
  can: (permission: string | undefined) => boolean;
}

const Context = createContext<AuthState | null>(null);
const EMPTY: ReadonlySet<string> = new Set();

export function AuthProvider({ children }: { children: ReactNode }) {
  const { setLanguage } = useI18n();
  const [loading, setLoading] = useState(() => getAccessToken() !== null);
  const [user, setUser] = useState<UserSummary | null>(null);
  const [permissions, setPermissions] = useState<ReadonlySet<string>>(EMPTY);
  const [modules, setModules] = useState<ReadonlySet<string>>(EMPTY);

  const reset = useCallback(() => {
    setUser(null);
    setPermissions(EMPTY);
    setModules(EMPTY);
  }, []);

  const reload = useCallback(async () => {
    if (!getAccessToken()) {
      reset();
      setLoading(false);
      return;
    }
    try {
      const me = await authApi.me();
      setUser(me.user);
      setLanguage(me.user.effectiveLanguageCode);
      setPermissions(new Set(me.permissions));
      setModules(new Set(me.modules));
    } catch {
      reset();
    } finally {
      setLoading(false);
    }
  }, [reset, setLanguage]);

  useEffect(() => { void reload(); }, [reload]);
  useEffect(() => onSignedOut(() => { reset(); setLoading(false); }), [reset]);

  const accept = useCallback(async (response: AuthResponse) => {
    setTokens(response.accessToken, response.refreshToken);
    await reload();
  }, [reload]);

  const value = useMemo<AuthState>(() => ({
    loading,
    user,
    permissions,
    modules,
    restricted: !!user && (user.mustChangePassword || user.mfaSetupRequired || user.emailConfirmationRequired),
    signIn: async (userName, password) => {
      const response = await authApi.login(userName, password);
      if (response.twoFactorRequired && response.twoFactorToken) {
        return { kind: 'two-factor', twoFactorToken: response.twoFactorToken };
      }
      await accept(response);
      return { kind: 'signed-in' };
    },
    completeTwoFactor: async (twoFactorToken, code) => accept(await authApi.loginTwoFactor(twoFactorToken, code)),
    signOut: async () => {
      try { await authApi.logout(); } catch { /* the session may already be gone */ }
      clearTokens();
      reset();
    },
    reload,
    switchCompany: async companyId => accept(await authApi.changeCompany(companyId)),
    can: permission => !permission || !!user?.isPlatformAdmin || permissions.has(permission),
  }), [loading, user, permissions, modules, reload, reset, accept]);

  return <Context.Provider value={value}>{children}</Context.Provider>;
}

export function useAuth(): AuthState {
  const value = useContext(Context);
  if (!value) throw new Error('useAuth needs an AuthProvider');
  return value;
}
