import { crud } from '@/services/http/crud';
import type { Audited } from '@/types/api';

export interface TenantRead extends Audited { name: string; code: string; languageCode: string | null }
export interface TenantWrite { name: string; code: string; languageCode?: string | null; isActive?: boolean }

export interface CompanyRead extends Audited {
  name: string;
  code: string;
  email: string | null;
  phoneNumber: string | null;
  website: string | null;
  address: string | null;
  address2: string | null;
  city: string | null;
  state: string | null;
  zip: string | null;
  countryId: number | null;
  languageCode: string | null;
  companyTypeId: number | null;
  tenantId: number | null;
}

export interface CompanyWrite {
  name: string;
  code: string;
  email?: string | null;
  phoneNumber?: string | null;
  website?: string | null;
  address?: string | null;
  address2?: string | null;
  city?: string | null;
  state?: string | null;
  zip?: string | null;
  countryId?: number | null;
  languageCode?: string | null;
  companyTypeId?: number | null;
  tenantId?: number | null;
  isActive?: boolean;
}

export interface CompanyTypeRead extends Audited { name: string; code: string; description: string | null }
export interface CountryRead extends Audited { name: string; code: string }
export interface LanguageRead extends Audited { name: string; code: string; nativeName: string | null; isFallback: boolean }

export const tenantsApi = crud<TenantRead, TenantWrite>('Tenant');
export const companiesApi = crud<CompanyRead, CompanyWrite>('Company');
export const companyTypesApi = crud<CompanyTypeRead, Partial<CompanyTypeRead>>('CompanyType');
export const countriesApi = crud<CountryRead, Partial<CountryRead>>('Country');
export const languagesApi = crud<LanguageRead, Partial<LanguageRead>>('Language');
