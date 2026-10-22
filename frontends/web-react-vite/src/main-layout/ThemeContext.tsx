import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';

export type Theme = 'light' | 'dark';
const KEY = 'fsi.theme';

function initialTheme(): Theme {
  try {
    const stored = localStorage.getItem(KEY);
    if (stored === 'light' || stored === 'dark') return stored;
  } catch { /* ignore */ }
  return window.matchMedia?.('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
}

const Context = createContext<{ theme: Theme; toggle: () => void } | null>(null);

export function ThemeProvider({ children }: { children: ReactNode }) {
  const [theme, setTheme] = useState<Theme>(initialTheme);

  useEffect(() => {
    document.documentElement.dataset.theme = theme;
    const link = document.getElementById('theme-link') as HTMLLinkElement | null;
    const href = `/themes/fs-${theme}.css`;
    if (link && !link.href.endsWith(href)) link.href = href;
    try { localStorage.setItem(KEY, theme); } catch { /* ignore */ }
  }, [theme]);

  const value = useMemo(() => ({ theme, toggle: () => setTheme(t => (t === 'dark' ? 'light' : 'dark')) }), [theme]);
  return <Context.Provider value={value}>{children}</Context.Provider>;
}

export function useTheme() {
  const value = useContext(Context);
  if (!value) throw new Error('useTheme needs a ThemeProvider');
  return value;
}
