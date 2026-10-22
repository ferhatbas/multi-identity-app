import { useCallback, useEffect, useRef, useState, type ReactNode } from 'react';
import { Column as PrimeColumn } from 'primereact/column';
import { DataTable as PrimeTable, type DataTableValue, type DataTableValueArray } from 'primereact/datatable';
import { IconField } from 'primereact/iconfield';
import { InputIcon } from 'primereact/inputicon';
import { InputText } from 'primereact/inputtext';
import type { PageQuery, Paged } from '@/types/api';
import { useT } from '@/i18n';
import { EmptyState } from './Form';

export interface Column<T> {
  key: string;
  header: string;
  sortKey?: string;
  render: (row: T) => ReactNode;
  csv?: (row: T) => string | number | boolean | null | undefined;
  className?: string;
}

export interface PagedTable<T> {
  rows: T[];
  total: number;
  totalPages: number;
  loading: boolean;
  error: string | null;
  query: PageQuery;
  setQuery: (patch: PageQuery) => void;
  setSearch: (text: string) => void;
  reload: () => void;
}

export function usePagedTable<T>(fetcher: (query: PageQuery) => Promise<Paged<T>>, initial: PageQuery = {}): PagedTable<T> {
  const [query, setQueryState] = useState<PageQuery>({ pageNumber: 1, pageSize: 25, ...initial });
  const [page, setPage] = useState<Paged<T> | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [tick, setTick] = useState(0);
  const latest = useRef(0);
  const fetcherRef = useRef(fetcher);
  fetcherRef.current = fetcher;

  useEffect(() => {
    const id = ++latest.current;
    setLoading(true);
    fetcherRef.current(query)
      .then(result => { if (id === latest.current) { setPage(result); setError(null); } })
      .catch((e: Error) => { if (id === latest.current) setError(e.message); })
      .finally(() => { if (id === latest.current) setLoading(false); });
  }, [query, tick]);

  const setQuery = useCallback((patch: PageQuery) => setQueryState(q => ({ ...q, ...patch })), []);
  const searchTimer = useRef<ReturnType<typeof setTimeout>>(undefined);
  const setSearch = useCallback((text: string) => {
    clearTimeout(searchTimer.current);
    searchTimer.current = setTimeout(() => setQueryState(q => ({ ...q, search: text || undefined, pageNumber: 1 })), 300);
  }, []);

  return {
    rows: page?.data ?? [],
    total: page?.totalRecords ?? 0,
    totalPages: page?.totalPages ?? 0,
    loading, error, query, setQuery, setSearch,
    reload: () => setTick(n => n + 1),
  };
}

export function SearchBox({ onChange, placeholder }: { onChange: (text: string) => void; placeholder?: string }) {
  const t = useT();
  return (
    <IconField iconPosition="left" className="search">
      <InputIcon className="pi pi-search" />
      <InputText type="search" placeholder={placeholder ?? t('Search')} onChange={e => onChange(e.target.value)} />
    </IconField>
  );
}

function csvCell(value: unknown): string {
  const text = value === null || value === undefined ? '' : String(value);
  const safe = /^[=+\-@]/.test(text) ? `'${text}` : text;
  return /[",\n]/.test(safe) ? `"${safe.replace(/"/g, '""')}"` : safe;
}

export function exportCsv<T>(columns: Column<T>[], rows: T[], fileName: string): void {
  const exportable = columns.filter(c => c.csv);
  const lines = [exportable.map(c => csvCell(c.header)).join(',')];
  for (const row of rows) lines.push(exportable.map(c => csvCell(c.csv!(row))).join(','));
  const blob = new Blob(['﻿' + lines.join('\r\n')], { type: 'text/csv;charset=utf-8' });
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  link.click();
  URL.revokeObjectURL(url);
}

interface DataTableProps<T> {
  columns: Column<T>[];
  table: PagedTable<T>;
  rowKey: (row: T) => string | number;
  actions?: (row: T) => ReactNode;
  onRowClick?: (row: T) => void;
  emptyTitle?: string;
  rowClass?: (row: T) => string | undefined;
}

export function DataTable<T>({ columns, table, rowKey, actions, onRowClick, emptyTitle, rowClass }: DataTableProps<T>) {
  const t = useT();
  const { query, rows } = table;
  const size = query.pageSize ?? 25;
  const page = query.pageNumber ?? 1;

  return (
    <div className="card table-card">
      {table.error && <div className="alert alert-error" style={{ borderRadius: 0 }}>{table.error}</div>}
      <PrimeTable
        value={rows as unknown as DataTableValueArray}
        lazy
        loading={table.loading}
        size="small"
        scrollable
        paginator
        first={(page - 1) * size}
        rows={size}
        totalRecords={table.total}
        rowsPerPageOptions={[12, 25, 50, 100]}
        paginatorTemplate="CurrentPageReport RowsPerPageDropdown FirstPageLink PrevPageLink PageLinks NextPageLink LastPageLink"
        currentPageReportTemplate={t('{from}–{to} of {total}', { from: '{first}', to: '{last}', total: '{totalRecords}' })}
        onPage={e => table.setQuery({ pageNumber: (e.page ?? 0) + 1, pageSize: e.rows })}
        sortField={query.orderBy}
        sortOrder={query.orderBy ? (query.desc ? -1 : 1) : undefined}
        onSort={e => table.setQuery({ orderBy: e.sortField, desc: e.sortOrder === -1, pageNumber: 1 })}
        onRowClick={onRowClick ? e => onRowClick(e.data as T) : undefined}
        rowClassName={(row: DataTableValue) => `${onRowClick ? 'clickable ' : ''}${rowClass?.(row as unknown as T) ?? ''}`}
        emptyMessage={table.loading ? '' : <EmptyState title={emptyTitle ?? t('Nothing here yet')} text={query.search ? t('Try another search.') : undefined} />}
      >
        {columns.map(c => (
          <PrimeColumn key={c.key} field={c.sortKey} header={c.header} sortable={!!c.sortKey} className={c.className}
                       body={(row: T) => c.render(row)} />
        ))}
        {actions && (
          <PrimeColumn key="actions" className="col-actions" frozen alignFrozen="right"
                       body={(row: T) => <div className="row" style={{ justifyContent: 'flex-end', gap: 2 }} onClick={e => e.stopPropagation()} data-key={rowKey(row)}>{actions(row)}</div>} />
        )}
      </PrimeTable>
    </div>
  );
}
