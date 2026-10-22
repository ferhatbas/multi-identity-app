import { createContext, useContext, useMemo, useRef, type ReactNode } from 'react';
import { Toast as PrimeToast } from 'primereact/toast';

interface Toasts {
  success: (text: string) => void;
  error: (text: string) => void;
  info: (text: string) => void;
}

const Context = createContext<Toasts | null>(null);

export function ToastProvider({ children }: { children: ReactNode }) {
  const ref = useRef<PrimeToast>(null);

  const value = useMemo<Toasts>(() => {
    const show = (severity: 'success' | 'error' | 'info', detail: string, life: number) =>
      ref.current?.show({ severity, detail, life });
    return {
      success: text => show('success', text, 4000),
      error: text => show('error', text, 7000),
      info: text => show('info', text, 4000),
    };
  }, []);

  return (
    <Context.Provider value={value}>
      {children}
      <PrimeToast ref={ref} position="bottom-right" />
    </Context.Provider>
  );
}

export function useToast(): Toasts {
  const value = useContext(Context);
  if (!value) throw new Error('useToast needs a ToastProvider');
  return value;
}
