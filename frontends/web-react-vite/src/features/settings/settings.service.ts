import { http } from '@/services/http/httpService';
import { crud } from '@/services/http/crud';
import type { Audited } from '@/types/api';

export interface SettingValue { key: string; value: string | null; source: 'user' | 'company' | 'tenant' }

export interface ScopedSetting extends Audited { key: string; value: string | null }

export const settingsApi = {
  mine: () => http.get<SettingValue[]>('/api/Settings/mine'),
  effective: (keys: string[]) => http.get<SettingValue[]>('/api/Settings/effective', { keys: keys.join(',') }),
  set: (key: string, value: string | null) => http.put<SettingValue>(`/api/Settings/mine/${encodeURIComponent(key)}`, { value }),
  remove: (key: string) => http.delete<boolean>(`/api/Settings/mine/${encodeURIComponent(key)}`),
  tenant: crud<ScopedSetting, { key: string; value: string | null }>('TenantSetting'),
  company: crud<ScopedSetting, { key: string; value: string | null }>('CompanySetting'),
};
