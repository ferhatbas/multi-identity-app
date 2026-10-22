export interface CompanyMembership {
  companyId: number;
  name: string;
  code: string;
  membershipType: number;
}

export interface UserSummary {
  id: number;
  userName: string;
  email: string;
  firstName: string;
  lastName: string;
  languageCode: string | null;
  effectiveLanguageCode: string;
  isPlatformAdmin: boolean;
  tenantId: number | null;
  activeCompanyId: number | null;
  activeApplicationTypeId: number | null;
  twoFactorEnabled: boolean;
  mustChangePassword: boolean;
  mfaSetupRequired: boolean;
  emailConfirmationRequired: boolean;
  companies: CompanyMembership[];
}

export interface AuthResponse {
  accessToken: string;
  refreshToken: string | null;
  tokenType: string;
  expiresAt: string;
  user: UserSummary;
  twoFactorRequired: boolean;
  twoFactorToken: string | null;
}

export interface MeResponse {
  user: UserSummary;
  modules: string[];
  permissions: string[];
}
