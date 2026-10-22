import { useMemo, useState, type FormEvent, type ReactNode } from 'react';
import { Button } from 'primereact/button';
import type { Audited, PageQuery, Paged } from '@/types/api';
import { ApiException } from '@/services/http/httpService';
import { useAuth } from '@/features/auth/AuthContext';
import { useI18n } from '@/i18n';
import { DataTable, SearchBox, exportCsv, usePagedTable, type Column } from './DataTable';
import { CheckField, PageHeader, SelectField, TextArea, TextField } from './Form';
import { ConfirmDialog, Modal } from './Modal';
import { useToast } from './Toast';

export interface FieldDef {
  name: string;
  label: string;
  type?: 'text' | 'email' | 'number' | 'password' | 'textarea' | 'select' | 'checkbox';
  required?: boolean;
  wide?: boolean;
  options?: { value: string | number; label: string }[];
  hint?: string;
  createOnly?: boolean;
  upper?: boolean;
  lower?: boolean;
  maxLength?: number;
}

interface ResourcePageProps<T extends Audited> {
  title: string;
  subtitle?: string;
  singular: string;
  api: {
    paged: (q: PageQuery) => Promise<Paged<T>>;
    create: (body: never) => Promise<T>;
    update: (id: number, body: never) => Promise<T>;
    remove: (id: number) => Promise<boolean>;
  };
  columns: Column<T>[];
  fields: FieldDef[];
  writePermission?: string;
  blank?: Record<string, unknown>;
  toForm?: (row: T) => Record<string, unknown>;
  toBody?: (values: Record<string, unknown>, existing: T | null) => unknown;
  extraActions?: (row: T, reload: () => void) => ReactNode;
  toolbarExtra?: ReactNode;
  initialQuery?: PageQuery;
  canEdit?: (row: T) => boolean;
  openRow?: (row: T) => void;
  exportName?: string;
  describeRow?: (row: T) => string;
}

function defaultBody(values: Record<string, unknown>): Record<string, unknown> {
  const body: Record<string, unknown> = {};
  for (const [k, v] of Object.entries(values)) body[k] = typeof v === 'string' && v.trim() === '' ? null : v;
  return body;
}

export function ResourcePage<T extends Audited>(props: ResourcePageProps<T>) {
  const { t } = useI18n();
  const { can } = useAuth();
  const toast = useToast();
  const table = usePagedTable<T>(props.api.paged, props.initialQuery);
  const canWrite = can(props.writePermission);
  const [editing, setEditing] = useState<{ row: T | null } | null>(null);
  const [deleting, setDeleting] = useState<T | null>(null);
  const [busy, setBusy] = useState(false);

  const columns = useMemo(() => props.columns, [props.columns]);

  const remove = async () => {
    if (!deleting) return;
    setBusy(true);
    try {
      await props.api.remove(deleting.id);
      toast.success(t('{what} deleted', { what: props.singular }));
      setDeleting(null);
      table.reload();
    } catch (e) {
      toast.error((e as Error).message);
    } finally {
      setBusy(false);
    }
  };

  return (
    <>
      <PageHeader
        title={props.title}
        subtitle={props.subtitle}
        actions={canWrite && (
          <Button icon="pi pi-plus" label={t('New {what}', { what: props.singular.toLowerCase() })} onClick={() => setEditing({ row: null })} />
        )}
      />
      <div className="toolbar">
        <SearchBox onChange={table.setSearch} />
        {props.toolbarExtra}
        <div className="grow" />
        <Button size="small" outlined severity="secondary" icon="pi pi-download" label={t('Export CSV')} disabled={table.rows.length === 0}
                onClick={() => exportCsv(columns, table.rows, `${props.exportName ?? props.title}.csv`)} />
        <Button text severity="secondary" icon="pi pi-refresh" aria-label={t('Refresh')} onClick={table.reload} />
      </div>

      <DataTable
        columns={columns}
        table={table}
        rowKey={r => r.id}
        rowClass={r => (r.isActive ? undefined : 'row-muted')}
        onRowClick={props.openRow}
        actions={row => (
          <div className="row" style={{ justifyContent: 'flex-end', gap: 2 }}>
            {props.extraActions?.(row, table.reload)}
            {canWrite && (props.canEdit?.(row) ?? true) && (
              <>
                <Button text rounded severity="secondary" icon="pi pi-pencil" onClick={() => (props.openRow ? props.openRow(row) : setEditing({ row }))} aria-label={t('Edit')} tooltip={t('Edit')} tooltipOptions={{ position: 'top' }} />
                <Button text rounded severity="danger" icon="pi pi-trash" onClick={() => setDeleting(row)} aria-label={t('Delete')} tooltip={t('Delete')} tooltipOptions={{ position: 'top' }} />
              </>
            )}
          </div>
        )}
      />

      {editing && (
        <RecordDialog
          {...props}
          row={editing.row}
          onClose={() => setEditing(null)}
          onSaved={() => { setEditing(null); table.reload(); }}
        />
      )}
      {deleting && (
        <ConfirmDialog
          title={t('Delete {what}', { what: props.singular.toLowerCase() })}
          message={t('Delete {name}? It can be restored later.', { name: props.describeRow?.(deleting) ?? `#${deleting.id}` })}
          confirmLabel={t('Delete')}
          danger busy={busy}
          onConfirm={() => void remove()}
          onCancel={() => setDeleting(null)}
        />
      )}
    </>
  );
}

function capitalise(text: string, language: string): string {
  return text.charAt(0).toLocaleUpperCase(language) + text.slice(1);
}

function RecordDialog<T extends Audited>(props: ResourcePageProps<T> & { row: T | null; onClose: () => void; onSaved: () => void }) {
  const { t, language } = useI18n();
  const toast = useToast();
  const { row } = props;
  const [values, setValues] = useState<Record<string, unknown>>(() =>
    row ? (props.toForm ? props.toForm(row) : { ...(row as unknown as Record<string, unknown>) }) : { ...(props.blank ?? {}) });
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [formError, setFormError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const set = (name: string, value: unknown) => setValues(v => ({ ...v, [name]: value }));

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setErrors({});
    setFormError(null);
    try {
      const body = props.toBody ? props.toBody(values, row) : defaultBody(values);
      if (row) await props.api.update(row.id, body as never);
      else await props.api.create(body as never);
      toast.success(t('{what} saved', { what: props.singular }));
      props.onSaved();
    } catch (err) {
      if (err instanceof ApiException) {
        const byField: Record<string, string> = {};
        for (const f of props.fields) {
          const m = err.fieldMessage(f.name);
          if (m) byField[f.name] = m;
        }
        setErrors(byField);
        if (Object.keys(byField).length === 0 || err.errors.some(x => !x.field || !byField[x.field])) setFormError(err.message);
      } else {
        setFormError((err as Error).message);
      }
    } finally {
      setBusy(false);
    }
  };

  return (
    <Modal
      title={capitalise(row ? t('Edit {what}', { what: props.singular.toLowerCase() }) : t('New {what}', { what: props.singular.toLowerCase() }), language)}
      onClose={props.onClose}
      width={620}
      footer={<>
        <Button type="button" outlined severity="secondary" label={t('Cancel')} onClick={props.onClose} disabled={busy} />
        <Button type="submit" form="record-form" label={busy ? t('Saving…') : t('Save')} loading={busy} />
      </>}
    >
      <form id="record-form" onSubmit={submit} className="stack">
        {formError && <div className="alert alert-error" role="alert">{formError}</div>}
        <div className="form-grid">
          {props.fields.map(f => {
            if (row && f.type === 'password') return null;
            const disabled = !!row && f.createOnly;
            const value = values[f.name];
            const common = { label: f.label, error: errors[f.name], wide: f.wide, hint: f.hint };
            switch (f.type) {
              case 'checkbox':
                return <div key={f.name} className="field-wide"><CheckField label={f.label} hint={f.hint} checked={!!value} onChange={v => set(f.name, v)} /></div>;
              case 'textarea':
                return <TextArea key={f.name} {...common} value={String(value ?? '')} maxLength={f.maxLength} onChange={e => set(f.name, e.target.value)} />;
              case 'select':
                return (
                  <SelectField key={f.name} {...common} required={f.required} disabled={disabled} placeholder={f.required ? undefined : '—'}
                               options={f.options ?? []} value={value === null || value === undefined ? '' : String(value)}
                               onChange={e => set(f.name, e.target.value === '' ? null : Number.isNaN(Number(e.target.value)) ? e.target.value : Number(e.target.value))} />
                );
              default:
                return (
                  <TextField key={f.name} {...common} type={f.type ?? 'text'} required={f.required} disabled={disabled} maxLength={f.maxLength}
                             value={String(value ?? '')} style={f.upper ? { textTransform: 'uppercase' } : undefined}
                             onChange={e => set(f.name, f.type === 'number' ? (e.target.value === '' ? null : Number(e.target.value)) : f.upper ? e.target.value.toUpperCase() : f.lower ? e.target.value.toLowerCase() : e.target.value)} />
                );
            }
          })}
        </div>
      </form>
    </Modal>
  );
}
