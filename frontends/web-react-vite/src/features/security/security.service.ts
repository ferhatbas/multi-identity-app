import { http } from '@/services/http/httpService';
import { crud } from '@/services/http/crud';
import type { Audited } from '@/types/api';

export interface EffectivePolicy {
  minPasswordLength: number;
  passwordRequiresLetter: boolean;
  passwordRequiresDigit: boolean;
  passwordRequiresSymbol: boolean;
  passwordRequiresMixedCase: boolean;
  passwordMaxAgeDays: number;
  maxFailedAttempts: number;
  lockoutWindowMinutes: number;
  lockoutMinutes: number;
  sessionDays: number;
  requireMfaFromRank: number | null;
  requireConfirmedEmail: boolean;
}

export interface SecurityPolicyRead extends Audited, Partial<EffectivePolicy> {
  name: string;
  tenantId: number | null;
}

export type SecurityPolicyWrite = Partial<EffectivePolicy> & { name: string; tenantId?: number | null; isActive?: boolean };

export const securityApi = {
  effective: () => http.get<EffectivePolicy>('/api/Security/effective-policy'),
  policies: crud<SecurityPolicyRead, SecurityPolicyWrite>('SecurityPolicy'),
};

export interface SoDRuleRead extends Audited { name: string; description: string | null; permissionAId: number; permissionBId: number; tenantId: number | null }
export interface SoDRuleWrite { name: string; description?: string | null; permissionAId: number; permissionBId: number; isActive?: boolean }

export const sodApi = crud<SoDRuleRead, SoDRuleWrite>('SoDRule');
