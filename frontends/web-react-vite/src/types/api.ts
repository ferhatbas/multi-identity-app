export interface ApiError {
  code: string;
  message: string;
  field: string | null;
}

export interface ApiResponse<T> {
  code: string;
  message: string;
  data: T;
  types: number;
  errors: ApiError[];
  timestamp: string;
  traceId: string;
}

export interface Paged<T> {
  pageNumber: number;
  pageSize: number;
  totalPages: number;
  totalRecords: number;
  data: T[];
}

export type QueryValue = string | number | boolean | undefined | null;

export interface PageQuery {
  pageNumber?: number;
  pageSize?: number;
  orderBy?: string;
  desc?: boolean;
  search?: string;
  [extra: string]: QueryValue;
}

export interface Audited {
  id: number;
  createdAt: string | null;
  createdBy: number | null;
  lastModifiedAt: string | null;
  lastModifiedBy: number | null;
  isDeleted: boolean;
  isActive: boolean;
}

export const UserType = { Reader: 0, Standard: 1, CompanyAdmin: 2, TenantAdmin: 3, PlatformAdmin: 4 } as const;
export const UserTypeNames = ['Reader', 'Standard', 'CompanyAdmin', 'TenantAdmin', 'PlatformAdmin'] as const;
