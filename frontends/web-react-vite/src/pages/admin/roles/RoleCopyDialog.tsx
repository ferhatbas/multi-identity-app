import { useEffect, useState } from 'react';
import { Button } from 'primereact/button';
import { Modal } from '@/components/ui/Modal';
import { SelectField, TextField } from '@/components/ui/Form';
import { useToast } from '@/components/ui/Toast';
import { rolesApi, type RoleRead } from '@/features/roles/roles.service';
import { useI18n } from '@/i18n';

export function RoleCopyDialog({ sourceId, onClose, onCreated }: { sourceId: number | null; onClose: () => void; onCreated: (role: RoleRead) => void }) {
  const { t } = useI18n();
  const toast = useToast();
  const [roles, setRoles] = useState<RoleRead[]>([]);
  const [source, setSource] = useState<number | null>(sourceId);
  const [name, setName] = useState('');
  const [code, setCode] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');

  useEffect(() => { rolesApi.list().then(setRoles).catch(e => toast.error((e as Error).message)); }, []); // eslint-disable-line react-hooks/exhaustive-deps

  const submit = async () => {
    if (!source) return;
    setBusy(true); setError('');
    try {
      const created = await rolesApi.copy(source, { name: name.trim(), code: code.trim().toUpperCase() || null });
      toast.success(t('Role copied'));
      onCreated(created);
    } catch (e) { setError((e as Error).message); } finally { setBusy(false); }
  };

  const picked = roles.find(r => r.id === source);

  return (
    <Modal title={t('Copy role')} onClose={onClose} width={560}
           footer={<>
             <Button text severity="secondary" label={t('Cancel')} onClick={onClose} disabled={busy} />
             <Button icon="pi pi-copy" label={t('Copy')} disabled={!source || !name.trim() || busy} loading={busy} onClick={() => void submit()} />
           </>}>
      <div className="stack">
        <SelectField label={t('Role to copy')} placeholder="—" value={source} options={roles.map(r => ({ value: r.id, label: r.name }))} onChange={e => setSource(e.target.value ? Number(e.target.value) : null)} />
        {picked && <div className="cell-sub">{picked.code ?? '—'}</div>}
        <div className="form-grid">
          <TextField label={t('New name')} required value={name} onChange={e => setName(e.target.value)} />
          <TextField label={t('New code')} value={code} maxLength={32} onChange={e => setCode(e.target.value.toUpperCase())} />
        </div>
        <div className="muted-text">{t('Every permission of the source role is copied. Change them on the new role afterwards.')}</div>
        {error && <div className="alert alert-error">{error}</div>}
      </div>
    </Modal>
  );
}
