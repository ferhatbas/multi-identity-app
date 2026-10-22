import { useRef } from 'react';
import { Button } from 'primereact/button';
import { Menu } from 'primereact/menu';
import type { MenuItem } from 'primereact/menuitem';
import { useT } from '@/i18n';

export function RowMenu({ items }: { items: MenuItem[] }) {
  const menu = useRef<Menu>(null);
  const t = useT();
  return (
    <>
      <Menu model={items} popup ref={menu} appendTo={document.body} />
      <Button text rounded severity="secondary" icon="pi pi-ellipsis-v" aria-label={t('More')} aria-haspopup
              onClick={e => menu.current?.toggle(e)} />
    </>
  );
}

const COLORS = ['#e11d74', '#7c3aed', '#2563eb', '#0891b2', '#059669', '#d97706', '#dc2626', '#4f46e5'];

export function Avatar({ id, text, size = 32 }: { id: number; text: string; size?: number }) {
  const initials = text.split(/\s+/).filter(Boolean).slice(0, 2).map(w => w[0]).join('').toUpperCase() || '?';
  return (
    <span className="avatar" style={{ width: size, height: size, background: `${COLORS[id % COLORS.length]}22`, color: COLORS[id % COLORS.length], fontSize: size * 0.38 }}>
      {initials}
    </span>
  );
}
