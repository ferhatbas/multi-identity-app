import { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Button } from 'primereact/button';
import { Column } from 'primereact/column';
import { TreeTable } from 'primereact/treetable';
import type { TreeNode } from 'primereact/treenode';
import { Badge, CheckField, PageHeader, TextField } from '@/components/ui/Form';
import { Modal } from '@/components/ui/Modal';
import { useToast } from '@/components/ui/Toast';
import { useAuth } from '@/features/auth/AuthContext';
import { P } from '@/features/auth/permissions';
import { companiesApi, tenantsApi, type CompanyRead, type TenantRead } from '@/features/tenants/organization.service';
import { unitsApi, type OrganizationUnit } from '@/features/units/units.service';
import { useI18n } from '@/i18n';

type Kind = 'tenant' | 'company' | 'unit';
interface OrgNode { kind: Kind; id: number; name: string; code: string; isActive: boolean; companyId?: number | null }

const WRITE: Record<Kind, string> = { tenant: P.TenantManage, company: P.CompanyManage, unit: P.UnitManage };

export function OrganizationPage() {
  const { t } = useI18n();
  const toast = useToast();
  const navigate = useNavigate();
  const { can, user } = useAuth();

  const [tenants, setTenants] = useState<TenantRead[]>([]);
  const [companies, setCompanies] = useState<CompanyRead[]>([]);
  const [units, setUnits] = useState<OrganizationUnit[]>([]);
  const [loading, setLoading] = useState(true);
  const [edit, setEdit] = useState<OrgNode | null>(null);
  const [saving, setSaving] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [tn, co, un] = await Promise.all([tenantsApi.list().catch(() => []), companiesApi.list().catch(() => []), unitsApi.organization().catch(() => [])]);
      setTenants(tn); setCompanies(co); setUnits(un);
    } catch (e) { toast.error((e as Error).message); } finally { setLoading(false); }
  }, []); // eslint-disable-line react-hooks/exhaustive-deps
  useEffect(() => { void load(); }, [load]);

  const nodes = useMemo<TreeNode[]>(() => {
    const unitNodes = (companyId: number): TreeNode[] => {
      const mine = units.filter(u => u.companyId === companyId);
      const build = (parentId: number | null): TreeNode[] => mine.filter(u => (u.parentId ?? null) === parentId)
        .map(u => ({ key: `u-${u.id}`, data: { kind: 'unit', id: u.id, name: u.name, code: u.code, isActive: u.isActive, companyId: u.companyId } as OrgNode, children: build(u.id) }));
      return build(null);
    };
    return tenants.map(tn => ({
      key: `t-${tn.id}`, data: { kind: 'tenant', id: tn.id, name: tn.name, code: tn.code, isActive: tn.isActive } as OrgNode,
      children: companies.filter(c => c.tenantId === tn.id).map(c => ({
        key: `c-${c.id}`, data: { kind: 'company', id: c.id, name: c.name, code: c.code, isActive: c.isActive } as OrgNode, children: unitNodes(c.id),
      })),
    }));
  }, [tenants, companies, units]);

  const kindBadge = (k: Kind) => (k === 'tenant' ? <Badge tone="info">{t('Tenant')}</Badge> : k === 'company' ? <Badge tone="success">{t('Company')}</Badge> : <Badge>{t('Unit')}</Badge>);

  const save = async () => {
    if (!edit) return;
    setSaving(true);
    try {
      if (edit.kind === 'tenant') {
        const s = tenants.find(x => x.id === edit.id)!;
        await tenantsApi.update(s.id, { name: edit.name.trim(), code: edit.code.trim().toUpperCase(), languageCode: s.languageCode, isActive: edit.isActive });
      } else if (edit.kind === 'company') {
        const c = companies.find(x => x.id === edit.id)!;
        await companiesApi.update(c.id, {
          name: edit.name.trim(), code: edit.code.trim().toUpperCase(), email: c.email, phoneNumber: c.phoneNumber, website: c.website, address: c.address, address2: c.address2,
          city: c.city, state: c.state, zip: c.zip, countryId: c.countryId, languageCode: c.languageCode, companyTypeId: c.companyTypeId, tenantId: c.tenantId, isActive: edit.isActive,
        });
      } else {
        const s = units.find(x => x.id === edit.id)!;
        await unitsApi.update(s.id, { code: edit.code.trim().toUpperCase(), name: edit.name.trim(), description: s.description, parentId: s.parentId, unitTypeId: s.unitTypeId, managerUserId: s.managerUserId, isActive: edit.isActive });
      }
      setEdit(null); toast.success(t('Saved')); await load();
    } catch (e) { toast.error((e as Error).message); } finally { setSaving(false); }
  };

  const addChild = (k: Kind) => navigate(k === 'tenant' ? '/admin/companies/new' : '/admin/units');

  return (
    <>
      <PageHeader title={t('Organisation hierarchy')} subtitle={t('Tenant → Company → Unit')}
                  actions={<Button text severity="secondary" icon="pi pi-refresh" aria-label={t('Refresh')} onClick={() => void load()} />} />
      <div className="card">
        <TreeTable value={nodes} loading={loading} tableStyle={{ minWidth: 640 }}>
          <Column expander header={t('Node')} body={(n: TreeNode) => { const d = n.data as OrgNode; return <span><b>{d.name}</b> <span className="muted-text">· {d.code}</span></span>; }} />
          <Column header={t('Level')} style={{ width: 130 }} body={(n: TreeNode) => kindBadge((n.data as OrgNode).kind)} />
          <Column header={t('Status')} style={{ width: 100 }} body={(n: TreeNode) => ((n.data as OrgNode).isActive ? <Badge tone="success">{t('Active')}</Badge> : <Badge>{t('Inactive')}</Badge>)} />
          <Column header="" style={{ width: 120 }} body={(n: TreeNode) => {
            const d = n.data as OrgNode;
            return (
              <div className="row">
                {can(WRITE[d.kind]) && (d.kind !== 'unit' || d.companyId === user?.activeCompanyId) && <Button text rounded severity="secondary" icon="pi pi-pencil" aria-label={t('Edit')} onClick={() => setEdit({ ...d })} />}
                {d.kind !== 'unit' && can(d.kind === 'tenant' ? P.CompanyManage : P.UnitManage) && <Button text rounded severity="info" icon="pi pi-plus" aria-label={t('Add child')} onClick={() => addChild(d.kind)} />}
              </div>
            );
          }} />
        </TreeTable>
      </div>

      {edit && (
        <Modal title={`${edit.name} — ${t('Edit')}`} onClose={() => setEdit(null)} width={460}
               footer={<><Button text severity="secondary" label={t('Cancel')} onClick={() => setEdit(null)} disabled={saving} /><Button label={t('Save')} loading={saving} disabled={!edit.name.trim() || !edit.code.trim()} onClick={() => void save()} /></>}>
          <div className="stack">
            <div>{kindBadge(edit.kind)}</div>
            <div className="form-grid">
              <TextField label={t('Name')} required value={edit.name} onChange={e => setEdit({ ...edit, name: e.target.value })} />
              <TextField label={t('Code')} required value={edit.code} onChange={e => setEdit({ ...edit, code: e.target.value.toUpperCase() })} />
              <CheckField label={t('Active')} checked={edit.isActive} onChange={v => setEdit({ ...edit, isActive: v })} />
            </div>
          </div>
        </Modal>
      )}
    </>
  );
}
