import { useCallback, useEffect, useMemo, useState } from 'react';
import { Button } from 'primereact/button';
import { Column } from 'primereact/column';
import { DataTable as PrimeTable } from 'primereact/datatable';
import { TreeTable } from 'primereact/treetable';
import type { TreeNode } from 'primereact/treenode';
import { Badge, CheckField, PageHeader, SelectField, TextField } from '@/components/ui/Form';
import { ConfirmDialog, Modal } from '@/components/ui/Modal';
import { useToast } from '@/components/ui/Toast';
import { useAuth } from '@/features/auth/AuthContext';
import { P } from '@/features/auth/permissions';
import { unitsApi, type UnitNode, type UnitRead, type UnitTypeRead, type UnitWrite, type UserUnitRead } from '@/features/units/units.service';
import { usersApi, type UserRead } from '@/features/users/users.service';
import { useI18n } from '@/i18n';

type TypeWrite = { code: string; name: string; isActive?: boolean };

const unitWrite = (u: UnitRead): UnitWrite => ({ code: u.code, name: u.name, description: u.description, parentId: u.parentId, unitTypeId: u.unitTypeId, managerUserId: u.managerUserId, isActive: u.isActive });

export function UnitsPage() {
  const { t } = useI18n();
  const toast = useToast();
  const { user, can } = useAuth();
  const canWrite = can(P.UnitManage);

  const [tree, setTree] = useState<UnitNode[]>([]);
  const [flat, setFlat] = useState<UnitRead[]>([]);
  const [types, setTypes] = useState<UnitTypeRead[]>([]);
  const [people, setPeople] = useState<UserRead[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);

  const [unitEdit, setUnitEdit] = useState<{ id: number | null; form: UnitWrite } | null>(null);
  const [typeEdit, setTypeEdit] = useState<{ id: number | null; form: TypeWrite } | null>(null);
  const [deleting, setDeleting] = useState<{ kind: 'unit' | 'type'; id: number; name: string } | null>(null);
  const [assign, setAssign] = useState<UnitNode | null>(null);
  const [members, setMembers] = useState<UserUnitRead[]>([]);
  const [pick, setPick] = useState<number | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [tr, fl, ty] = await Promise.all([
        user?.activeCompanyId ? unitsApi.tree() : Promise.resolve([]), user?.activeCompanyId ? unitsApi.list() : Promise.resolve([]), unitsApi.types.list(),
      ]);
      setTree(tr); setFlat(fl); setTypes(ty);
    } catch (e) { toast.error((e as Error).message); } finally { setLoading(false); }
  }, [user?.activeCompanyId]); // eslint-disable-line react-hooks/exhaustive-deps
  useEffect(() => { void load(); }, [load]);

  useEffect(() => {
    if (!user?.activeCompanyId) { setPeople([]); return; }
    Promise.all([usersApi.list(), usersApi.memberships.list()]).then(([users, ms]) => {
      const ids = new Set(ms.filter(m => m.companyId === user.activeCompanyId).map(m => m.userId));
      setPeople(users.filter(u => ids.has(u.id)));
    }).catch(() => setPeople([]));
  }, [user?.activeCompanyId]);

  const nodes = useMemo<TreeNode[]>(() => {
    const toNode = (n: UnitNode): TreeNode => ({ key: String(n.id), data: n, children: n.children.map(toNode) });
    return tree.map(toNode);
  }, [tree]);

  const typeName = (id: number) => types.find(x => x.id === id)?.name ?? `#${id}`;
  const personName = (id: number | null) => { const p = people.find(x => x.id === id); return p ? `${p.firstName} ${p.lastName}`.trim() || p.userName : id ? `#${id}` : ''; };
  const num = (v: string) => (v ? Number(v) : null);

  const saveUnit = async () => {
    if (!unitEdit) return;
    setSaving(true);
    try {
      if (unitEdit.id) await unitsApi.update(unitEdit.id, unitEdit.form); else await unitsApi.create(unitEdit.form);
      setUnitEdit(null); toast.success(t('Saved')); await load();
    } catch (e) { toast.error((e as Error).message); } finally { setSaving(false); }
  };
  const saveType = async () => {
    if (!typeEdit) return;
    setSaving(true);
    try {
      if (typeEdit.id) await unitsApi.types.update(typeEdit.id, typeEdit.form); else await unitsApi.types.create(typeEdit.form);
      setTypeEdit(null); toast.success(t('Saved')); await load();
    } catch (e) { toast.error((e as Error).message); } finally { setSaving(false); }
  };
  const remove = async () => {
    if (!deleting) return;
    setSaving(true);
    try { await (deleting.kind === 'unit' ? unitsApi.remove(deleting.id) : unitsApi.types.remove(deleting.id)); setDeleting(null); await load(); }
    catch (e) { toast.error((e as Error).message); } finally { setSaving(false); }
  };

  const askDeleteUnit = (n: UnitNode) => {
    if (n.children.length > 0) { toast.error(t('First delete or move the sub-units')); return; }
    setDeleting({ kind: 'unit', id: n.id, name: n.name });
  };

  const openAssign = async (n: UnitNode) => {
    setAssign(n); setPick(null);
    try { setMembers((await unitsApi.members.list()).filter(m => m.unitId === n.id)); } catch (e) { toast.error((e as Error).message); }
  };
  const refreshMembers = async () => { if (assign) setMembers((await unitsApi.members.list()).filter(m => m.unitId === assign.id)); };
  const addMember = async () => {
    if (!assign || !pick) return;
    setSaving(true);
    try { await unitsApi.members.create({ userId: pick, unitId: assign.id, isPrimary: false }); setPick(null); await refreshMembers(); }
    catch (e) { toast.error((e as Error).message); } finally { setSaving(false); }
  };
  const removeMember = async (m: UserUnitRead) => {
    setSaving(true);
    try { await unitsApi.members.remove(m.id); await refreshMembers(); } catch (e) { toast.error((e as Error).message); } finally { setSaving(false); }
  };

  const unitForm = unitEdit?.form;
  const setUnit = (patch: Partial<UnitWrite>) => setUnitEdit(u => (u ? { ...u, form: { ...u.form, ...patch } } : u));
  const typeForm = typeEdit?.form;
  const setType = (patch: Partial<TypeWrite>) => setTypeEdit(x => (x ? { ...x, form: { ...x.form, ...patch } } : x));
  const cancel = (close: () => void) => <Button text severity="secondary" label={t('Cancel')} onClick={close} disabled={saving} />;
  const statusBadge = (active: boolean) => (active ? <Badge tone="success">{t('Active')}</Badge> : <Badge>{t('Inactive')}</Badge>);

  return (
    <>
      <PageHeader title={t('Units')} subtitle={t('{n} units in total', { n: flat.length })}
                  actions={<>
                    <Button text severity="secondary" icon="pi pi-refresh" aria-label={t('Refresh')} onClick={() => void load()} />
                    {canWrite && <Button icon="pi pi-plus" label={t('New unit')} disabled={!user?.activeCompanyId || types.length === 0}
                                         onClick={() => setUnitEdit({ id: null, form: { code: '', name: '', description: '', parentId: null, unitTypeId: types[0]?.id ?? 0, managerUserId: null, isActive: true } })} />}
                  </>} />

      {!user?.activeCompanyId && <div className="alert alert-warning" style={{ marginBottom: 14 }}>{t('Choose a company first: units belong to the company you are working in.')}</div>}

      <div className="card" style={{ marginBottom: 16 }}>
        <TreeTable value={nodes} loading={loading} tableStyle={{ minWidth: 700 }} className="p-treetable-sm">
          <Column expander header={t('Unit')} body={(n: TreeNode) => { const d = n.data as UnitNode; return <span><b>{d.name}</b> <span className="muted-text">· {d.code}</span></span>; }} />
          <Column header={t('Type')} style={{ width: 180 }} body={(n: TreeNode) => typeName((n.data as UnitNode).unitTypeId)} />
          <Column header={t('Manager')} style={{ width: 200 }} body={(n: TreeNode) => personName((n.data as UnitNode).managerUserId) || <span className="muted-text">—</span>} />
          <Column header={t('Status')} style={{ width: 100 }} body={(n: TreeNode) => statusBadge(flat.find(u => u.id === (n.data as UnitNode).id)?.isActive ?? true)} />
          <Column header="" style={{ width: 150 }} body={(n: TreeNode) => {
            const d = n.data as UnitNode;
            const full = flat.find(u => u.id === d.id);
            return canWrite ? (
              <div className="row">
                <Button text rounded severity="info" icon="pi pi-users" aria-label={t('Assign users')} onClick={() => void openAssign(d)} />
                <Button text rounded severity="secondary" icon="pi pi-pencil" aria-label={t('Edit')} onClick={() => full && setUnitEdit({ id: full.id, form: unitWrite(full) })} />
                <Button text rounded severity="danger" icon="pi pi-trash" aria-label={t('Delete')} onClick={() => askDeleteUnit(d)} />
              </div>
            ) : null;
          }} />
        </TreeTable>
      </div>

      <div className="card">
        <div className="toolbar" style={{ padding: '12px 16px 0' }}>
          <h3>{t('Unit types')}</h3>
          <div className="grow" />
          {canWrite && <Button size="small" outlined icon="pi pi-plus" label={t('New type')} onClick={() => setTypeEdit({ id: null, form: { code: '', name: '', isActive: true } })} />}
        </div>
        <PrimeTable value={types} size="small" emptyMessage={t('No unit types yet')}>
          <Column header={t('Name')} body={(x: UnitTypeRead) => <b>{x.name}</b>} />
          <Column header={t('Code')} body={(x: UnitTypeRead) => <code>{x.code}</code>} />
          <Column header="" style={{ width: 110 }} body={(x: UnitTypeRead) => (canWrite ? (
            <div className="row">
              <Button text rounded severity="secondary" icon="pi pi-pencil" aria-label={t('Edit')} onClick={() => setTypeEdit({ id: x.id, form: { code: x.code, name: x.name, isActive: x.isActive } })} />
              <Button text rounded severity="danger" icon="pi pi-trash" aria-label={t('Delete')} onClick={() => setDeleting({ kind: 'type', id: x.id, name: x.name })} />
            </div>
          ) : null)} />
        </PrimeTable>
      </div>

      {unitEdit && unitForm && (
        <Modal title={unitEdit.id ? t('Edit unit') : t('New unit')} onClose={() => setUnitEdit(null)} width={520}
               footer={<>{cancel(() => setUnitEdit(null))}<Button label={t('Save')} loading={saving} disabled={!unitForm.name.trim() || !unitForm.code.trim() || !unitForm.unitTypeId} onClick={() => void saveUnit()} /></>}>
          <div className="form-grid">
            <TextField label={t('Name')} required value={unitForm.name} onChange={e => setUnit({ name: e.target.value })} />
            <TextField label={t('Code')} required value={unitForm.code} hint={t('Unique within the company; upper case.')} onChange={e => setUnit({ code: e.target.value.toUpperCase() })} />
            <SelectField label={t('Type')} required value={unitForm.unitTypeId} options={types.map(x => ({ value: x.id, label: x.name }))} onChange={e => setUnit({ unitTypeId: Number(e.target.value) })} />
            <SelectField label={t('Parent unit')} placeholder={t('Root unit')} value={unitForm.parentId} options={flat.filter(u => u.id !== unitEdit.id).map(u => ({ value: u.id, label: `${u.name} (${u.code})` }))}
                         hint={t('A unit cannot be moved under itself or one of its own units.')} onChange={e => setUnit({ parentId: num(e.target.value) })} />
            <SelectField label={t('Manager')} placeholder="—" value={unitForm.managerUserId} options={people.map(p => ({ value: p.id, label: personName(p.id) }))}
                         hint={t('Must be a member of this company.')} onChange={e => setUnit({ managerUserId: num(e.target.value) })} />
            <TextField wide label={t('Description')} value={unitForm.description ?? ''} maxLength={512} onChange={e => setUnit({ description: e.target.value || null })} />
            <CheckField label={t('Active')} checked={unitForm.isActive ?? true} onChange={v => setUnit({ isActive: v })} />
          </div>
        </Modal>
      )}

      {typeEdit && typeForm && (
        <Modal title={typeEdit.id ? t('Edit type') : t('New type')} onClose={() => setTypeEdit(null)} width={460}
               footer={<>{cancel(() => setTypeEdit(null))}<Button label={t('Save')} loading={saving} disabled={!typeForm.name.trim() || !typeForm.code.trim()} onClick={() => void saveType()} /></>}>
          <div className="form-grid">
            <TextField label={t('Name')} required value={typeForm.name} maxLength={128} onChange={e => setType({ name: e.target.value })} />
            <TextField label={t('Code')} required value={typeForm.code} maxLength={32} onChange={e => setType({ code: e.target.value.toUpperCase() })} />
            <CheckField label={t('Active')} checked={typeForm.isActive ?? true} onChange={v => setType({ isActive: v })} />
          </div>
        </Modal>
      )}

      {assign && (
        <Modal title={`${assign.name} — ${t('Assign users')}`} onClose={() => setAssign(null)} width={520}>
          <div className="stack">
            <div className="row" style={{ alignItems: 'flex-end' }}>
              <div className="grow">
                <SelectField label={t('User')} placeholder={t('Select user')} value={pick}
                             options={people.filter(p => !members.some(m => m.userId === p.id)).map(p => ({ value: p.id, label: personName(p.id) }))} onChange={e => setPick(num(e.target.value))} />
              </div>
              <Button icon="pi pi-plus" label={t('Add')} disabled={!pick || saving} loading={saving} onClick={() => void addMember()} />
            </div>
            <PrimeTable value={members} size="small" emptyMessage={t('No users assigned to this unit')}>
              <Column header={t('User')} body={(m: UserUnitRead) => <b>{personName(m.userId)}</b>} />
              <Column header={t('Primary')} style={{ width: 90 }} body={(m: UserUnitRead) => (m.isPrimary ? <Badge tone="success">✓</Badge> : null)} />
              <Column header="" style={{ width: 60 }} body={(m: UserUnitRead) => <Button text rounded severity="danger" icon="pi pi-trash" aria-label={t('Remove')} disabled={saving} onClick={() => void removeMember(m)} />} />
            </PrimeTable>
          </div>
        </Modal>
      )}

      {deleting && <ConfirmDialog title={t('Delete')} message={t('Delete {name}? It can be restored later.', { name: deleting.name })} confirmLabel={t('Delete')} danger busy={saving} onCancel={() => setDeleting(null)} onConfirm={() => void remove()} />}
    </>
  );
}
