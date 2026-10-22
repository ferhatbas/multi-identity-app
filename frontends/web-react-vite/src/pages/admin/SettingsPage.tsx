import { useCallback, useEffect, useMemo, useState, type FormEvent } from 'react';
import { Column as PrimeColumn } from 'primereact/column';
import { DataTable as PrimeTable } from 'primereact/datatable';
import { TabBar } from '@/components/ui/Tabs';
import { ResourcePage, type FieldDef } from '@/components/ui/ResourcePage';
import type { Column } from '@/components/ui/DataTable';
import { EmptyState, PageHeader, Spinner, TextField } from '@/components/ui/Form';
import { Icon } from '@/components/ui/Icon';
import { useToast } from '@/components/ui/Toast';
import { useAuth } from '@/features/auth/AuthContext';
import { P } from '@/features/auth/permissions';
import { settingsApi, type ScopedSetting, type SettingValue } from '@/features/settings/settings.service';
import { useI18n } from '@/i18n';

function MySettings() {
  const { t } = useI18n();
  const toast = useToast();
  const [items, setItems] = useState<SettingValue[] | null>(null);
  const [key, setKey] = useState('');
  const [value, setValue] = useState('');

  const load = useCallback(() => { settingsApi.mine().then(setItems).catch((e: Error) => toast.error(e.message)); }, [toast]);
  useEffect(load, [load]);

  const add = async (e: FormEvent) => {
    e.preventDefault();
    try {
      await settingsApi.set(key.trim(), value);
      setKey(''); setValue('');
      load();
    } catch (err) {
      toast.error((err as Error).message);
    }
  };

  if (!items) return <Spinner label={t('Loading…')} />;

  return (
    <div className="stack" style={{ maxWidth: 680 }}>
      <p className="muted-text">{t('Preferences only you see. They win over your company’s and your tenant’s values.')}</p>
      <form className="row row-wrap" style={{ alignItems: 'flex-end' }} onSubmit={add}>
        <TextField label={t('Key')} value={key} onChange={e => setKey(e.target.value)} placeholder="ui.density" required />
        <TextField label={t('Value')} value={value} onChange={e => setValue(e.target.value)} />
        <button className="p-button" disabled={!key.trim()}><Icon name="plus" size={16} />{t('Save')}</button>
      </form>
      {items.length === 0 ? <EmptyState title={t('No personal settings yet')} /> : (
        <div className="card table-card">
          <PrimeTable value={items} size="small">
            <PrimeColumn header={t('Key')} body={(s: SettingValue) => <code>{s.key}</code>} />
            <PrimeColumn header={t('Value')} body={(s: SettingValue) => s.value ?? '—'} />
            <PrimeColumn className="col-actions" body={(s: SettingValue) => (
              <button className="p-button p-button-text p-button-rounded p-button-danger p-button-icon-only" aria-label={t('Delete')} onClick={() => void settingsApi.remove(s.key).then(load).catch((e: Error) => toast.error(e.message))}><Icon name="trash" /></button>
            )} />
          </PrimeTable>
        </div>
      )}
    </div>
  );
}

function ScopedSettings({ scope }: { scope: 'tenant' | 'company' }) {
  const { t, formatDate } = useI18n();
  const api = scope === 'tenant' ? settingsApi.tenant : settingsApi.company;

  const columns = useMemo<Column<ScopedSetting>[]>(() => [
    { key: 'key', header: t('Key'), sortKey: 'key', render: r => <code>{r.key}</code>, csv: r => r.key },
    { key: 'value', header: t('Value'), render: r => r.value ?? '—', csv: r => r.value },
    { key: 'changed', header: t('Changed'), render: r => formatDate(r.lastModifiedAt ?? r.createdAt) },
  ], [t, formatDate]);

  const fields: FieldDef[] = [
    { name: 'key', label: t('Key'), required: true, maxLength: 64, hint: t('Letters, digits, dot, dash and underscore.') },
    { name: 'value', label: t('Value'), type: 'textarea', wide: true, maxLength: 4000 },
  ];

  return (
    <ResourcePage<ScopedSetting>
      key={scope}
      title={scope === 'tenant' ? t('Tenant settings') : t('Company settings')} singular={t('Setting')}
      api={{ paged: api.paged, create: api.create as never, update: api.update as never, remove: api.remove }}
      columns={columns} fields={fields} writePermission={P.SettingsManage}
      toForm={r => ({ key: r.key, value: r.value })} describeRow={r => r.key} exportName={`${scope}-settings`}
    />
  );
}

export function SettingsPage() {
  const { t } = useI18n();
  const { can } = useAuth();
  const [tab, setTab] = useState<'mine' | 'company' | 'tenant'>('mine');
  const manage = can(P.SettingsManage);

  return (
    <>
      <PageHeader title={t('Settings')} subtitle={t('Three layers: yours, your company’s, your tenant’s. The nearest wins.')} />
      <TabBar tabs={[{ id: 'mine', label: t('Mine') }, ...(manage ? [{ id: 'company' as const, label: t('Company') }, { id: 'tenant' as const, label: t('Tenant') }] : [])]} value={tab} onChange={setTab} />
      {tab === 'mine' && <MySettings />}
      {tab === 'company' && <ScopedSettings scope="company" />}
      {tab === 'tenant' && <ScopedSettings scope="tenant" />}
    </>
  );
}
