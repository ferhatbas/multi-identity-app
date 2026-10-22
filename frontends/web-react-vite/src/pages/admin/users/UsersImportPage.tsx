import { useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Button } from 'primereact/button';
import { Column as PrimeColumn } from 'primereact/column';
import { DataTable as PrimeTable } from 'primereact/datatable';
import { Badge, CheckField, PageHeader, SelectField } from '@/components/ui/Form';
import { useToast } from '@/components/ui/Toast';
import { useAuth } from '@/features/auth/AuthContext';
import { useOptions } from '@/features/lookups/useOptions';
import { companiesApi } from '@/features/tenants/organization.service';
import { usersApi, type BulkUserImportResult, type BulkUserRow } from '@/features/users/users.service';
import { UserType } from '@/types/api';
import { useI18n } from '@/i18n';

const REQUIRED = ['userName', 'email', 'firstName', 'lastName'] as const;
const TEMPLATE = 'userName,email,firstName,lastName,password\r\nada.lovelace,ada@example.com,Ada,Lovelace,\r\nalan.turing,alan@example.com,Alan,Turing,\r\n';

interface Row extends BulkUserRow { line: number; problem: string | null }

function parseCsv(text: string): string[][] {
  const rows: string[][] = [];
  let row: string[] = [];
  let cell = '';
  let quoted = false;
  for (let i = 0; i < text.length; i++) {
    const c = text[i];
    if (quoted) {
      if (c === '"' && text[i + 1] === '"') { cell += '"'; i++; } else if (c === '"') quoted = false; else cell += c;
    } else if (c === '"') quoted = true;
    else if (c === ',') { row.push(cell); cell = ''; }
    else if (c === '\n') { row.push(cell); rows.push(row); row = []; cell = ''; }
    else if (c !== '\r') cell += c;
  }
  if (cell !== '' || row.length) { row.push(cell); rows.push(row); }
  return rows.filter(r => r.some(x => x.trim() !== ''));
}

export function UsersImportPage() {
  const { t } = useI18n();
  const toast = useToast();
  const navigate = useNavigate();
  const { user } = useAuth();
  const input = useRef<HTMLInputElement>(null);
  const [rows, setRows] = useState<Row[]>([]);
  const [companyId, setCompanyId] = useState<number | null>(user?.activeCompanyId ?? null);
  const [atomic, setAtomic] = useState(false);
  const [busy, setBusy] = useState(false);
  const [result, setResult] = useState<BulkUserImportResult | null>(null);
  const companies = useOptions(() => companiesApi.list(), r => ({ value: r.id, label: r.name }));

  const load = async (file: File) => {
    const table = parseCsv(await file.text());
    setResult(null);
    if (table.length < 2) { toast.error(t('The file needs a header row and at least one user.')); setRows([]); return; }
    const head = table[0].map(h => h.trim());
    const col = (name: string) => head.findIndex(h => h.toLowerCase() === name.toLowerCase());
    setRows(table.slice(1).map((cells, i) => {
      const get = (name: string) => (cells[col(name)] ?? '').trim();
      const missing = REQUIRED.filter(c => col(c) < 0 || !get(c));
      const problem = missing.length ? t('missing: {list}', { list: missing.join(', ') }) : !get('email').includes('@') ? t('the e-mail must contain @') : null;
      return { line: i + 2, problem, userName: get('userName'), email: get('email'), firstName: get('firstName'), lastName: get('lastName'), password: get('password') || null };
    }));
  };

  const valid = rows.filter(r => !r.problem);

  const run = async () => {
    setBusy(true);
    try {
      const res = await usersApi.importUsers(valid.map(({ line: _l, problem: _p, ...r }) => ({ ...r, companyId, membershipType: UserType.Standard, roleIds: [] })), atomic);
      setResult(res);
      toast[res.failed.length ? 'error' : 'success'](t('{created} created, {failed} failed', { created: res.created, failed: res.failed.length }));
    } catch (e) { toast.error((e as Error).message); } finally { setBusy(false); }
  };

  const download = () => {
    const url = URL.createObjectURL(new Blob(['﻿' + TEMPLATE], { type: 'text/csv;charset=utf-8' }));
    const a = document.createElement('a');
    a.href = url; a.download = 'users-template.csv'; a.click();
    URL.revokeObjectURL(url);
  };

  return (
    <>
      <PageHeader title={t('Import users')} subtitle={t('Create many users from a CSV file.')}
                  actions={<>
                    <Button text severity="secondary" icon="pi pi-download" label={t('Download template')} onClick={download} />
                    <Button outlined severity="secondary" icon="pi pi-arrow-left" label={t('Back')} onClick={() => navigate('/admin/users')} />
                  </>} />
      <div className="stack" style={{ maxWidth: 1000 }}>
        <div className="card card-pad row row-wrap">
          <strong>1. {t('Choose a file')}</strong>
          <input ref={input} type="file" accept=".csv,text/csv" hidden onChange={e => { const f = e.target.files?.[0]; if (f) void load(f); e.target.value = ''; }} />
          <Button outlined severity="secondary" icon="pi pi-upload" label={t('Choose file')} onClick={() => input.current?.click()} />
          {rows.length > 0 && <span className="muted-text">{t('{n} rows, {valid} valid', { n: rows.length, valid: valid.length })}</span>}
          <span className="muted-text">{t('Columns: userName, email, firstName, lastName, password (optional).')}</span>
        </div>

        {rows.length > 0 && (
          <div className="card card-pad stack">
            <strong>2. {t('Check and run')}</strong>
            <div className="row row-wrap" style={{ alignItems: 'flex-end' }}>
              <div style={{ width: 260 }}>
                <SelectField label={t('Add everyone to company')} placeholder="—" value={companyId} options={companies.options}
                             onChange={e => setCompanyId(e.target.value === '' ? null : Number(e.target.value))} />
              </div>
              <CheckField label={t('All or nothing')} hint={t('If any row is wrong, nothing is imported.')} checked={atomic} onChange={setAtomic} />
              <div className="grow" />
              <Button icon="pi pi-play" label={t('Run import ({n} valid)', { n: valid.length })} disabled={valid.length === 0 || busy} loading={busy} onClick={() => void run()} />
            </div>
            <PrimeTable value={rows} size="small" scrollable scrollHeight="360px">
              <PrimeColumn header="#" body={(r: Row) => r.line} style={{ width: 50 }} />
              <PrimeColumn header={t('User name')} field="userName" />
              <PrimeColumn header={t('E-mail')} field="email" />
              <PrimeColumn header={t('Name')} body={(r: Row) => `${r.firstName} ${r.lastName}`} />
              <PrimeColumn header={t('Check')} style={{ width: 200 }} body={(r: Row) => {
                const failure = result?.failed.find(f => f.row === r.line - 1 || f.row === r.line);
                const ok = result?.users.find(u => u.userName === r.userName.toLowerCase());
                return failure ? <Badge tone="danger">{failure.error}</Badge> : ok ? <Badge tone="success">OK</Badge> : r.problem ? <Badge tone="warning">{r.problem}</Badge> : <Badge tone="info">OK</Badge>;
              }} />
            </PrimeTable>
          </div>
        )}

        {result && (
          <div className="stack">
            {result.rolledBack && <div className="alert alert-error">{t('Nothing was imported: a row was wrong.')}</div>}
            {result.users.some(u => u.temporaryPassword) && (
              <div className="alert alert-warning">
                {t('Generated passwords (shown once):')}
                <pre className="recovery" style={{ display: 'block' }}>{result.users.filter(u => u.temporaryPassword).map(u => `${u.userName}: ${u.temporaryPassword}`).join('\n')}</pre>
              </div>
            )}
            {result.failed.length > 0 && <div className="alert alert-error">{result.failed.map(f => <div key={f.row}>{t('Row {row}', { row: f.row })}: {f.userName}: {f.error}</div>)}</div>}
            <div><Button label={t('Back to users')} onClick={() => navigate('/admin/users')} /></div>
          </div>
        )}
      </div>
    </>
  );
}
