import { crud } from '@/services/http/crud';
import type { Audited } from '@/types/api';

export interface CountryRead extends Audited {
  name: string; nameGlobal: string | null; code: string; region: string | null; formatCode: string; languageCode: string | null;
  dateFormat: string | null; dateTimeFormat: string | null; currencyCode: string | null; currencySymbol: string | null; priceFormat: string | null; timeZone: string | null;
}
export interface StateRead extends Audited { code: string; name: string; countryId: number; description: string | null }
export interface CityRead extends Audited { code: string; name: string; stateId: number; description: string | null }
export interface LanguageRead extends Audited { code: string; name: string; nativeName: string | null; isRtl: boolean; displayOrder: number; isFallback: boolean }
export interface CompanyTypeRead extends Audited { code: string; name: string; description: string | null }
export interface ComponentTypeRead extends Audited { code: string; name: string; description: string | null }
export interface MenuTypeRead extends Audited { code: string; name: string }
export interface MenuFeatureToggleRead extends Audited { featureCode: string; isEnabled: boolean }
export interface MenuPermissionOverrideRead extends Audited { menuId: number; permissionId: number | null }
export interface ApplicationTypeRead extends Audited { code: string; name: string; description: string | null; icon: string | null; displayOrder: number }
export interface AppModuleRead extends Audited { code: string; name: string; description: string | null; displayOrder: number; isCore: boolean }
export interface CompanyTypeApplicationRead extends Audited { companyTypeId: number; applicationTypeId: number; isDefault: boolean }
export interface ApplicationModuleRead extends Audited { applicationTypeId: number; appModuleId: number }

type Body<T> = Partial<Omit<T, keyof Audited>> & { isActive?: boolean };
const make = <T extends Audited>(resource: string) => crud<T, Body<T>>(resource);

export const definitionsApi = {
  countries: make<CountryRead>('Country'),
  states: make<StateRead>('State'),
  cities: make<CityRead>('City'),
  languages: make<LanguageRead>('Language'),
  companyTypes: make<CompanyTypeRead>('CompanyType'),
  componentTypes: make<ComponentTypeRead>('ComponentType'),
  menuTypes: make<MenuTypeRead>('MenuType'),
  menuToggles: make<MenuFeatureToggleRead>('MenuFeatureToggle'),
  menuOverrides: make<MenuPermissionOverrideRead>('MenuPermissionOverride'),
  applicationTypes: make<ApplicationTypeRead>('ApplicationType'),
  appModules: make<AppModuleRead>('AppModule'),
  companyTypeApplications: make<CompanyTypeApplicationRead>('CompanyTypeApplicationType'),
  applicationModules: make<ApplicationModuleRead>('ApplicationTypeModule'),
};
