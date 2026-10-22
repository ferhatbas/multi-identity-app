import { Fragment, useEffect, useMemo, useState } from 'react';
import { Button } from 'primereact/button';
import { Calendar } from 'primereact/calendar';
import { Dropdown } from 'primereact/dropdown';
import { DataTable, SearchBox, exportCsv, usePagedTable, type Column } from '@/components/ui/DataTable';
import { Badge, PageHeader } from '@/components/ui/Form';
import { Modal } from '@/components/ui/Modal';
import { useToast } from '@/components/ui/Toast';
import { auditApi, type AuditLogRead } from '@/features/audit/audit.service';
import { useI18n } from '@/i18n';

const TONE: Record<string, 'success' | 'info' | 'danger' | 'warning' | 'neutral'> = {
  Create: 'success', Update: 'info', Delete: 'danger', Restore: 'warning',
};

const EXPORT_LIMIT = 10000;

function parse(json: string | null): Record<string, unknown> {
  if (!json) return {};
  try {
    const value = JSON.parse(json);
    return value && typeof value === 'object' && !Array.isArray(value) ? (value as Record<string, unknown>) : {};
  } catch {
    return {};
  }
}

const show = (v: unknown) => (v === undefined ? '' : v === null ? 'null' : typeof v === 'object' ? JSON.stringify(v) : String(v));
const day = (d: Date) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;

function AuditDetail({ row, onClose }: { row: AuditLogRead; onClose: () => void }) {
  const { t, formatDate } = useI18n();
  const before = parse(row.oldValues);
  const after = parse(row.newValues);
  const fields = [...new Set([...Object.keys(before), ...Object.keys(after)])];

  return (
    <Modal title={`${row.action} · ${row.entityName} ${row.entityId ?? ''}`} onClose={onClose} width={760}
           footer={<Button label={t('Close')} onClick={onClose} />}>
      <dl className="kv" style={{ marginBottom: 18 }}>
        <dt>{t('Action')}</dt><dd><Badge tone={TONE[row.action] ?? 'neutral'}>{row.action}</Badge></dd>
        <dt>{t('Record')}</dt><dd><code>{row.entityName}{row.entityId ? ` #${row.entityId}` : ''}</code></dd>
        <dt>{t('Who')}</dt><dd>{row.userName ?? '—'}{row.userId ? <span className="muted-text"> (#{row.userId})</span> : null}</dd>
        <dt>{t('When')}</dt><dd>{formatDate(row.createdAt)}</dd>
        <dt>{t('From')}</dt><dd>{row.ipAddress ?? '—'}</dd>
        <dt>{t('Request')}</dt><dd><code>{row.correlationId ?? '—'}</code></dd>
        <dt>{t('Browser')}</dt><dd className="cell-sub" title={row.userAgent ?? ''}>{row.userAgent ?? '—'}</dd>
      </dl>
      {fields.length === 0 ? <p className="muted-text">{t('No values were recorded.')}</p> : (
        <div className="diff">
          <div className="diff-head">{t('Field')}</div><div className="diff-head">{t('Before')}</div><div className="diff-head">{t('After')}</div>
          {fields.map(f => (
            <Fragment key={f}>
              <div><code>{f}</code></div>
              <div className={f in before ? 'old' : ''}>{show(before[f])}</div>
              <div className={f in after ? 'new' : ''}>{show(after[f])}</div>
            </Fragment>
          ))}
        </div>
      )}
    </Modal>
  );
}

export function AuditPage() {
  const { t, formatDate } = useI18n();
  const toast = useToast();
  const table = usePagedTable<AuditLogRead>(auditApi.paged, { pageSize: 25 });
  const [open, setOpen] = useState<AuditLogRead | null>(null);
  const [actions, setActions] = useState<string[]>([]);
  const [action, setAction] = useState<string | null>(null);
  const [range, setRange] = useState<(Date | null)[] | null>(null);
  const [exporting, setExporting] = useState(false);

  useEffect(() => { auditApi.actions().then(setActions).catch(() => undefined); }, []);

  const columns = useMemo<Column<AuditLogRead>[]>(() => [
    { key: 'when', header: t('When'), render: r => formatDate(r.createdAt), csv: r => r.createdAt },
    { key: 'who', header: t('Who'), render: r => (r.userName ? <code>{r.userName}</code> : <span className="muted-text">system</span>), csv: r => r.userName },
    { key: 'action', header: t('Action'), render: r => <Badge tone={TONE[r.action] ?? 'neutral'}>{r.action}</Badge>, csv: r => r.action },
    {
      key: 'entity', header: t('Record'), csv: r => `${r.entityName} ${r.entityId ?? ''}`,
      render: r => <><span className="cell-main">{r.entityName}</span> <span className="muted-text">#{r.entityId ?? '—'}</span></>,
    },
    { key: 'changed', header: t('Changed'), render: r => <span className="cell-sub">{(r.changedFields ?? '').replace(/[[\]"]/g, '').replace(/,/g, ', ')}</span>, csv: r => r.changedFields },
    { key: 'ip', header: t('From'), render: r => r.ipAddress ?? '—', csv: r => r.ipAddress },
  ], [t, formatDate]);

  const filters = (): Record<string, string | undefined> => {
    const [from, to] = range ?? [null, null];
    return { search: table.query.search, action: action ?? undefined, from: from ? day(from) : undefined, to: to ? day(to) : undefined };
  };

  const exportAll = async () => {
    setExporting(true);
    try {
      const rows: AuditLogRead[] = [];
      for (let pageNumber = 1; rows.length < EXPORT_LIMIT; pageNumber++) {
        const page = await auditApi.paged({ ...filters(), pageNumber, pageSize: 500 });
        rows.push(...page.data);
        if (pageNumber >= page.totalPages) break;
      }
      exportCsv(columns, rows, 'audit.csv');
      toast.success(t('Exported {n} rows', { n: rows.length }));
    } catch (e) { toast.error((e as Error).message); } finally { setExporting(false); }
  };

  return (
    <>
      <PageHeader title={t('Audit trail')} subtitle={t('Every change, who made it and when. Read-only.')} />
      <div className="toolbar">
        <SearchBox onChange={table.setSearch} placeholder={t('Search record, person or field…')} />
        <Dropdown value={action} aria-label={t('Action')} style={{ width: 170 }} placeholder={t('Any action')} showClear appendTo="self"
                  options={actions.map(a => ({ label: a, value: a }))}
                  onChange={e => { const v = (e.value ?? null) as string | null; setAction(v); table.setQuery({ action: v ?? undefined, pageNumber: 1 }); }} />
        <Calendar value={range as Date[] | null} selectionMode="range" readOnlyInput hideOnRangeSelection showButtonBar appendTo="self"
                  placeholder={t('Date range')} style={{ width: 230 }} dateFormat="dd.mm.yy"
                  onChange={e => {
                    const next = (e.value as (Date | null)[] | null) ?? null;
                    setRange(next);
                    const [from, to] = next ?? [null, null];
                    if (next === null || (from && to)) table.setQuery({ from: from ? day(from) : undefined, to: to ? day(to) : undefined, pageNumber: 1 });
                  }} />
        <div className="grow" />
        <Button size="small" outlined severity="secondary" icon="pi pi-download" label={t('Export CSV')} loading={exporting} disabled={table.rows.length === 0} onClick={() => void exportAll()} />
        <Button text severity="secondary" icon="pi pi-refresh" aria-label={t('Refresh')} onClick={table.reload} />
      </div>
      <DataTable columns={columns} table={table} rowKey={r => r.id} onRowClick={setOpen} emptyTitle={t('No changes recorded')} />
      {open && <AuditDetail row={open} onClose={() => setOpen(null)} />}
    </>
  );
}
