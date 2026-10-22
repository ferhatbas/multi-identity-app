import { http } from '@/services/http/httpService';

export interface PreferenceView {
  category: string;
  channel: string;
  enabled: boolean;
  mandatory: boolean;
  isDefault: boolean;
}

export interface PushTokenView { id: number; platform: string; deviceName: string | null; lastSeenAt: string | null; createdAt: string | null }

export const notificationsApi = {
  preferences: () => http.get<PreferenceView[]>('/api/Notification/preferences'),
  save: (items: { category: string; channel: string; enabled: boolean }[]) =>
    http.put<PreferenceView[]>('/api/Notification/preferences', { items }),
  devices: () => http.get<PushTokenView[]>('/api/Notification/push-tokens'),
  removeDevice: (id: number) => http.delete<boolean>(`/api/Notification/push-tokens/${id}`),
};
