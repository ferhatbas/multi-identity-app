import type { Audited, PageQuery, Paged } from '@/types/api';
import { http } from './httpService';

export function crud<TRead extends Audited, TWrite = Partial<TRead>>(resource: string) {
  const base = `/api/${resource}`;
  return {
    paged: (query: PageQuery = {}) => http.get<Paged<TRead>>(`${base}/paged`, query),
    list: () => http.get<TRead[]>(base),
    deleted: () => http.get<TRead[]>(`${base}/deleted`),
    get: (id: number) => http.get<TRead>(`${base}/${id}`),
    create: (body: TWrite) => http.post<TRead>(base, body),
    update: (id: number, body: TWrite) => http.put<TRead>(`${base}/${id}`, body),
    remove: (id: number) => http.delete<boolean>(`${base}/${id}`),
    restore: (id: number) => http.post<TRead>(`${base}/${id}/restore`),
  };
}
