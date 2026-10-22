import { TabMenu } from 'primereact/tabmenu';

export interface TabDef<T extends string> { id: T; label: string }

export function TabBar<T extends string>({ tabs, value, onChange }: { tabs: TabDef<T>[]; value: T; onChange: (id: T) => void }) {
  return (
    <TabMenu model={tabs.map(x => ({ label: x.label }))} activeIndex={Math.max(0, tabs.findIndex(x => x.id === value))}
             onTabChange={e => onChange(tabs[e.index].id)} style={{ marginBottom: 14 }} />
  );
}
