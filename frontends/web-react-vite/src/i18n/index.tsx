import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';

const loaders = import.meta.glob<Record<string, string>>('./locales/*.json', { import: 'default' });

export type Language = string;

const codeOf = (path: string) => /\/([^/]+)\.json$/.exec(path)![1];
const nameOf = (code: string) => {
  try {
    const name = new Intl.DisplayNames([code], { type: 'language' }).of(code) ?? code;
    return name.charAt(0).toLocaleUpperCase(code) + name.slice(1);
  } catch { return code; }
};

export const LANGUAGES: { code: Language; label: string }[] = Object.keys(loaders)
  .map(codeOf)
  .sort((a, b) => (a === 'en' ? -1 : b === 'en' ? 1 : a.localeCompare(b)))
  .map(code => ({ code, label: nameOf(code) }));

const supported = (code: string | null | undefined): code is Language => !!code && LANGUAGES.some(l => l.code === code);

const KEY = 'fsi.lang';

function initialLanguage(): Language {
  try {
    const stored = localStorage.getItem(KEY);
    if (supported(stored)) return stored;
  } catch { /* ignore */ }
  const preferred = navigator.language?.toLowerCase().slice(0, 2);
  return supported(preferred) ? preferred : 'en';
}

type Vars = Record<string, string | number>;

interface I18n {
  language: Language;
  setLanguage: (language: string | null | undefined) => void;
  t: (text: string, vars?: Vars) => string;
  formatDate: (value: string | null | undefined, withTime?: boolean) => string;
}

const Context = createContext<I18n | null>(null);

export function I18nProvider({ children }: { children: ReactNode }) {
  const [language, setLanguageState] = useState<Language>(initialLanguage);
  const [dictionary, setDictionary] = useState<Record<string, string>>({});

  useEffect(() => {
    let live = true;
    document.documentElement.lang = language;
    const load = Object.entries(loaders).find(([path]) => codeOf(path) === language)?.[1];
    void (load ? load() : Promise.resolve({})).then(d => { if (live) setDictionary(d); }).catch(() => { if (live) setDictionary({}); });
    return () => { live = false; };
  }, [language]);

  const setLanguage = useCallback((next: string | null | undefined) => {
    if (!supported(next)) return;
    setLanguageState(next);
    try { localStorage.setItem(KEY, next); } catch { /* ignore */ }
  }, []);

  const value = useMemo<I18n>(() => ({
    language,
    setLanguage,
    t: (text, vars) => {
      const raw = dictionary[text] ?? text;
      return vars ? raw.replace(/\{(\w+)\}/g, (_, name: string) => String(vars[name] ?? `{${name}}`)) : raw;
    },
    formatDate: (value, withTime = true) => {
      if (!value) return '—';
      const date = new Date(value);
      if (Number.isNaN(date.getTime())) return value;
      const locale = language === 'en' ? 'en-GB' : language;
      return new Intl.DateTimeFormat(locale, withTime ? { dateStyle: 'medium', timeStyle: 'short' } : { dateStyle: 'medium' }).format(date);
    },
  }), [language, dictionary, setLanguage]);

  return <Context.Provider value={value}>{children}</Context.Provider>;
}

export function useI18n(): I18n {
  const value = useContext(Context);
  if (!value) throw new Error('useI18n needs an I18nProvider');
  return value;
}

export const useT = () => useI18n().t;
