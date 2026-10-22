import { http } from '@/services/http/httpService';
import type { PageQuery, Paged } from '@/types/api';

export interface AuditLogRead {
  id: number;
  createdAt: string;
  entityName: string;
  entityId: string | null;
  action: string;
  userId: number | null;
  userName: string | null;
  ipAddress: string | null;
  userAgent: string | null;
  correlationId: string | null;
  oldValues: string | null;
  newValues: string | null;
  changedFields: string | null;
}

export const auditApi = {
  paged: (query: PageQuery) => http.get<Paged<AuditLogRead>>('/api/AuditLog/paged', query),
  actions: () => http.get<string[]>('/api/AuditLog/actions'),
  get: (id: number) => http.get<AuditLogRead>(`/api/AuditLog/${id}`),
};
