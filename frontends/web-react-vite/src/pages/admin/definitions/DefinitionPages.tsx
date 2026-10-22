import { useMemo } from 'react';
import { ResourcePage, type FieldDef } from '@/components/ui/ResourcePage';
import type { Column } from '@/components/ui/DataTable';
import { Badge } from '@/components/ui/Form';
import { P } from '@/features/auth/permissions';
import {
  definitionsApi, type CityRead, type CompanyTypeRead, type ComponentTypeRead, type CountryRead, type LanguageRead,
  type MenuFeatureToggleRead, type MenuPermissionOverrideRead, type MenuTypeRead, type StateRead,
} from '@/features/definitions/definitions.service';
import { menusApi } from '@/features/menus/menus.service';
import { useOptions } from '@/features/lookups/useOptions';
import { rolesApi } from '@/features/roles/roles.service';
import { useI18n } from '@/i18n';

function useStatus() {
  const { t } = useI18n();
  return (active: boolean) => (active ? <Badge tone="success">{t('Active')}</Badge> : <Badge>{t('Inactive')}</Badge>);
}

const name = <T extends { name: string }>(label: string): Column<T> => ({ key: 'name', header: label, sortKey: 'name', csv: r => r.name, render: r => <b>{r.name}</b> });
const code = <T extends { code: string }>(label: string): Column<T> => ({ key: 'code', header: label, sortKey: 'code', csv: r => r.code, render: r => <code>{r.code}</code> });

export function CompanyTypesPage() {
  const { t } = useI18n();
  const status = useStatus();
  const columns = useMemo<Column<CompanyTypeRead>[]>(() => [
    name(t('Name')), code(t('Code')),
    { key: 'description', header: t('Description'), csv: r => r.description, render: r => r.description || '—' },
    { key: 'status', header: t('Status'), csv: r => r.isActive, render: r => status(r.isActive) },
  // eslint-disable-next-line react-hooks/exhaustive-deps
  ], [t]);
  const fields: FieldDef[] = [
    { name: 'name', label: t('Name'), required: true, maxLength: 256 },
    { name: 'code', label: t('Code'), required: true, upper: true, maxLength: 32 },
    { name: 'description', label: t('Description'), type: 'textarea', wide: true, maxLength: 512 },
    { name: 'isActive', label: t('Active'), type: 'checkbox' },
  ];
  return (
    <ResourcePage<CompanyTypeRead> title={t('Company types')} subtitle={t('What kind of business a company is. It decides which applications the company uses.')} singular={t('Company type')}
      api={definitionsApi.companyTypes} columns={columns} fields={fields} writePermission={P.CompanyTypeManage} blank={{ isActive: true }} describeRow={r => r.name} exportName="company-types" />
  );
}

export function ComponentTypesPage() {
  const { t } = useI18n();
  const status = useStatus();
  const columns = useMemo<Column<ComponentTypeRead>[]>(() => [
    name(t('Name')), code(t('Code')),
    { key: 'description', header: t('Description'), csv: r => r.description, render: r => r.description || '—' },
    { key: 'status', header: t('Status'), csv: r => r.isActive, render: r => status(r.isActive) },
  // eslint-disable-next-line react-hooks/exhaustive-deps
  ], [t]);
  const fields: FieldDef[] = [
    { name: 'name', label: t('Name'), required: true, maxLength: 256 },
    { name: 'code', label: t('Code'), required: true, upper: true, maxLength: 32 },
    { name: 'description', label: t('Description'), type: 'textarea', wide: true, maxLength: 512 },
    { name: 'isActive', label: t('Active'), type: 'checkbox' },
  ];
  return (
    <ResourcePage<ComponentTypeRead> title={t('Component types')} subtitle={t('The kinds of block a dashboard is made of.')} singular={t('Component type')}
      api={definitionsApi.componentTypes} columns={columns} fields={fields} writePermission={P.DashboardManage} blank={{ isActive: true }} describeRow={r => r.name} exportName="component-types" />
  );
}

export function CountriesPage() {
  const { t } = useI18n();
  const status = useStatus();
  const columns = useMemo<Column<CountryRead>[]>(() => [
    name(t('Name')), code(t('Code')),
    { key: 'region', header: t('Region'), sortKey: 'region', csv: r => r.region, render: r => r.region || '—' },
    { key: 'currency', header: t('Currency'), csv: r => r.currencyCode, render: r => r.currencyCode || '—' },
    { key: 'timeZone', header: t('Time zone'), csv: r => r.timeZone, render: r => r.timeZone || '—' },
    { key: 'status', header: t('Status'), csv: r => r.isActive, render: r => status(r.isActive) },
  // eslint-disable-next-line react-hooks/exhaustive-deps
  ], [t]);
  const fields: FieldDef[] = [
    { name: 'name', label: t('Name'), required: true, maxLength: 256 },
    { name: 'code', label: t('Code'), required: true, upper: true, maxLength: 8, hint: t('For example TR.') },
    { name: 'nameGlobal', label: t('International name'), maxLength: 256 },
    { name: 'region', label: t('Region'), maxLength: 128 },
    { name: 'formatCode', label: t('Format code'), required: true, maxLength: 16 },
    { name: 'languageCode', label: t('Language'), lower: true, maxLength: 8 },
    { name: 'currencyCode', label: t('Currency code'), upper: true, maxLength: 8 },
    { name: 'currencySymbol', label: t('Currency symbol'), maxLength: 8 },
    { name: 'priceFormat', label: t('Price format'), maxLength: 32 },
    { name: 'dateFormat', label: t('Date format'), maxLength: 32 },
    { name: 'dateTimeFormat', label: t('Date and time format'), maxLength: 32 },
    { name: 'timeZone', label: t('Time zone'), maxLength: 64 },
    { name: 'isActive', label: t('Active'), type: 'checkbox' },
  ];
  return (
    <ResourcePage<CountryRead> title={t('Countries')} subtitle={t('Country, then region, then city.')} singular={t('Country')}
      api={definitionsApi.countries} columns={columns} fields={fields} writePermission={P.GeographyManage} blank={{ isActive: true }} describeRow={r => r.name} exportName="countries" />
  );
}

export function StatesPage() {
  const { t } = useI18n();
  const status = useStatus();
  const countries = useOptions(() => definitionsApi.countries.list(), r => ({ value: r.id, label: r.name }));
  const columns = useMemo<Column<StateRead>[]>(() => [
    name(t('Name')), code(t('Code')),
    { key: 'country', header: t('Country'), sortKey: 'countryId', csv: r => countries.options.find(o => o.value === r.countryId)?.label, render: r => countries.options.find(o => o.value === r.countryId)?.label ?? `#${r.countryId}` },
    { key: 'status', header: t('Status'), csv: r => r.isActive, render: r => status(r.isActive) },
  // eslint-disable-next-line react-hooks/exhaustive-deps
  ], [t, countries.options]);
  const fields: FieldDef[] = [
    { name: 'name', label: t('Name'), required: true, maxLength: 256 },
    { name: 'code', label: t('Code'), required: true, upper: true, maxLength: 32 },
    { name: 'countryId', label: t('Country'), type: 'select', required: true, createOnly: true, options: countries.options },
    { name: 'description', label: t('Description'), type: 'textarea', wide: true, maxLength: 512 },
    { name: 'isActive', label: t('Active'), type: 'checkbox' },
  ];
  return (
    <ResourcePage<StateRead> title={t('Regions')} subtitle={t('The level between a country and its cities.')} singular={t('Region')}
      api={definitionsApi.states} columns={columns} fields={fields} writePermission={P.GeographyManage} blank={{ isActive: true }} describeRow={r => r.name} exportName="regions" />
  );
}

export function CitiesPage() {
  const { t } = useI18n();
  const status = useStatus();
  const states = useOptions(() => definitionsApi.states.list(), r => ({ value: r.id, label: `${r.name} (${r.code})` }));
  const columns = useMemo<Column<CityRead>[]>(() => [
    name(t('Name')), code(t('Code')),
    { key: 'state', header: t('Region'), sortKey: 'stateId', csv: r => states.options.find(o => o.value === r.stateId)?.label, render: r => states.options.find(o => o.value === r.stateId)?.label ?? `#${r.stateId}` },
    { key: 'status', header: t('Status'), csv: r => r.isActive, render: r => status(r.isActive) },
  // eslint-disable-next-line react-hooks/exhaustive-deps
  ], [t, states.options]);
  const fields: FieldDef[] = [
    { name: 'name', label: t('Name'), required: true, maxLength: 256 },
    { name: 'code', label: t('Code'), required: true, upper: true, maxLength: 32 },
    { name: 'stateId', label: t('Region'), type: 'select', required: true, createOnly: true, options: states.options },
    { name: 'description', label: t('Description'), type: 'textarea', wide: true, maxLength: 512 },
    { name: 'isActive', label: t('Active'), type: 'checkbox' },
  ];
  return (
    <ResourcePage<CityRead> title={t('Cities')} subtitle={t('The smallest place in the geography.')} singular={t('City')}
      api={definitionsApi.cities} columns={columns} fields={fields} writePermission={P.GeographyManage} blank={{ isActive: true }} describeRow={r => r.name} exportName="cities" />
  );
}

export function LanguagesPage() {
  const { t } = useI18n();
  const status = useStatus();
  const columns = useMemo<Column<LanguageRead>[]>(() => [
    { key: 'name', header: t('Name'), sortKey: 'name', csv: r => r.name, render: r => <><div className="cell-main">{r.name}</div><div className="cell-sub">{r.nativeName ?? ''}</div></> },
    code(t('Code')),
    { key: 'order', header: t('Order'), sortKey: 'displayOrder', csv: r => r.displayOrder, render: r => r.displayOrder },
    { key: 'fallback', header: t('Fallback'), csv: r => r.isFallback, render: r => (r.isFallback ? <Badge tone="info">{t('Fallback')}</Badge> : '—') },
    { key: 'rtl', header: t('Right to left'), csv: r => r.isRtl, render: r => (r.isRtl ? '✓' : '—') },
    { key: 'status', header: t('Status'), csv: r => r.isActive, render: r => status(r.isActive) },
  // eslint-disable-next-line react-hooks/exhaustive-deps
  ], [t]);
  const fields: FieldDef[] = [
    { name: 'name', label: t('Name'), required: true, maxLength: 128 },
    { name: 'code', label: t('Code'), required: true, lower: true, maxLength: 8, hint: t('A two-letter language code, e.g. en or tr.') },
    { name: 'nativeName', label: t('Name in its own language'), maxLength: 128 },
    { name: 'displayOrder', label: t('Order'), type: 'number' },
    { name: 'isFallback', label: t('Fallback language'), type: 'checkbox', hint: t('Shown when a translation is missing. Only one language is the fallback.') },
    { name: 'isRtl', label: t('Right to left'), type: 'checkbox' },
    { name: 'isActive', label: t('Active'), type: 'checkbox' },
  ];
  return (
    <ResourcePage<LanguageRead> title={t('Languages')} subtitle={t('The languages people can use the application in.')} singular={t('Language')}
      api={definitionsApi.languages} columns={columns} fields={fields} writePermission={P.LanguageManage} blank={{ isActive: true, displayOrder: 0, isFallback: false, isRtl: false }}
      describeRow={r => r.name} exportName="languages" />
  );
}

export function MenuTypesPage() {
  const { t } = useI18n();
  const status = useStatus();
  const columns = useMemo<Column<MenuTypeRead>[]>(() => [
    name(t('Name')), code(t('Code')),
    { key: 'status', header: t('Status'), csv: r => r.isActive, render: r => status(r.isActive) },
  // eslint-disable-next-line react-hooks/exhaustive-deps
  ], [t]);
  const fields: FieldDef[] = [
    { name: 'name', label: t('Name'), required: true, maxLength: 128 },
    { name: 'code', label: t('Code'), required: true, upper: true, maxLength: 32 },
    { name: 'isActive', label: t('Active'), type: 'checkbox' },
  ];
  return (
    <ResourcePage<MenuTypeRead> title={t('Menu types')} subtitle={t('Which menu an entry belongs to: the main menu, a toolbar...')} singular={t('Menu type')}
      api={definitionsApi.menuTypes} columns={columns} fields={fields} writePermission={P.MenuDesign} blank={{ isActive: true }} describeRow={r => r.name} exportName="menu-types" />
  );
}

export function MenuTogglesPage() {
  const { t } = useI18n();
  const status = useStatus();
  const columns = useMemo<Column<MenuFeatureToggleRead>[]>(() => [
    { key: 'feature', header: t('Feature'), sortKey: 'featureCode', csv: r => r.featureCode, render: r => <code>{r.featureCode}</code> },
    { key: 'enabled', header: t('Switched on'), csv: r => r.isEnabled, render: r => (r.isEnabled ? <Badge tone="success">{t('On')}</Badge> : <Badge>{t('Off')}</Badge>) },
    { key: 'status', header: t('Status'), csv: r => r.isActive, render: r => status(r.isActive) },
  // eslint-disable-next-line react-hooks/exhaustive-deps
  ], [t]);
  const fields: FieldDef[] = [
    { name: 'featureCode', label: t('Feature code'), required: true, upper: true, maxLength: 64, hint: t('Menu entries with this feature code show only while it is on.') },
    { name: 'isEnabled', label: t('Switched on'), type: 'checkbox' },
    { name: 'isActive', label: t('Active'), type: 'checkbox' },
  ];
  return (
    <ResourcePage<MenuFeatureToggleRead> title={t('Menu feature switches')} subtitle={t('Switch whole groups of menu entries on or off.')} singular={t('Feature switch')}
      api={definitionsApi.menuToggles} columns={columns} fields={fields} writePermission={P.MenuManage} blank={{ isActive: true, isEnabled: true }} describeRow={r => r.featureCode} exportName="menu-feature-switches" />
  );
}

export function MenuOverridesPage() {
  const { t } = useI18n();
  const status = useStatus();
  const menus = useOptions(() => menusApi.list(), r => ({ value: r.id, label: `${r.title} (${r.code})` }));
  const perms = useOptions(() => rolesApi.permissions.list(), r => ({ value: r.id, label: r.code }));
  const columns = useMemo<Column<MenuPermissionOverrideRead>[]>(() => [
    { key: 'menu', header: t('Menu entry'), csv: r => menus.options.find(o => o.value === r.menuId)?.label, render: r => <b>{menus.options.find(o => o.value === r.menuId)?.label ?? `#${r.menuId}`}</b> },
    { key: 'permission', header: t('Required permission'), csv: r => perms.options.find(o => o.value === r.permissionId)?.label, render: r => (r.permissionId ? <code>{perms.options.find(o => o.value === r.permissionId)?.label ?? `#${r.permissionId}`}</code> : <span className="muted-text">{t('None')}</span>) },
    { key: 'status', header: t('Status'), csv: r => r.isActive, render: r => status(r.isActive) },
  // eslint-disable-next-line react-hooks/exhaustive-deps
  ], [t, menus.options, perms.options]);
  const fields: FieldDef[] = [
    { name: 'menuId', label: t('Menu entry'), type: 'select', required: true, createOnly: true, options: menus.options },
    { name: 'permissionId', label: t('Required permission'), type: 'select', options: perms.options, hint: t('Empty makes the entry open to everyone in your tenant.') },
    { name: 'isActive', label: t('Active'), type: 'checkbox' },
  ];
  return (
    <ResourcePage<MenuPermissionOverrideRead> title={t('Menu permission overrides')} subtitle={t('Change which permission a menu entry asks for, for your tenant.')} singular={t('Override')}
      api={definitionsApi.menuOverrides} columns={columns} fields={fields} writePermission={P.MenuManage} blank={{ isActive: true }} describeRow={r => `#${r.id}`} exportName="menu-overrides" />
  );
}
