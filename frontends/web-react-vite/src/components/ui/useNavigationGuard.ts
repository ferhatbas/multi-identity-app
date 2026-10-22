import { useEffect } from 'react';

export function useNavigationGuard(active: boolean, message: string): void {
  useEffect(() => {
    if (!active) return;

    const beforeUnload = (e: BeforeUnloadEvent) => { e.preventDefault(); };

    const click = (e: MouseEvent) => {
      if (e.defaultPrevented || e.button !== 0 || e.metaKey || e.ctrlKey || e.shiftKey || e.altKey) return;
      const link = (e.target as Element | null)?.closest?.('a[href]') as HTMLAnchorElement | null;
      if (!link || link.target === '_blank' || link.origin !== window.location.origin) return;
      if (link.pathname === window.location.pathname && link.search === window.location.search) return;
      if (!window.confirm(message)) { e.preventDefault(); e.stopPropagation(); }
    };

    let leaving = false;
    history.pushState({ guarded: true }, '', window.location.href);
    const popState = () => {
      if (leaving) return;
      if (window.confirm(message)) { leaving = true; history.back(); } else history.pushState({ guarded: true }, '', window.location.href);
    };

    window.addEventListener('beforeunload', beforeUnload);
    document.addEventListener('click', click, true);
    window.addEventListener('popstate', popState);
    return () => {
      window.removeEventListener('beforeunload', beforeUnload);
      document.removeEventListener('click', click, true);
      window.removeEventListener('popstate', popState);
    };
  }, [active, message]);
}
