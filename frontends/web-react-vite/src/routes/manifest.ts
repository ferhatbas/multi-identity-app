
export interface RouteEntry { path: string; permission: string[] }

export const ROUTES: RouteEntry[] = [
  {
    "path": "/account",
    "permission": []
  },
  {
    "path": "/account/security",
    "permission": []
  },
  {
    "path": "/admin/access",
    "permission": [
      "ROLE_MANAGE"
    ]
  },
  {
    "path": "/admin/application-types",
    "permission": [
      "APPTYPE_MANAGE"
    ]
  },
  {
    "path": "/admin/audit",
    "permission": [
      "AUDIT_LOG_VIEW"
    ]
  },
  {
    "path": "/admin/companies",
    "permission": [
      "COMPANY_MANAGE",
      "TENANT_VIEW"
    ]
  },
  {
    "path": "/admin/companies/:id",
    "permission": [
      "COMPANY_MANAGE",
      "TENANT_VIEW"
    ]
  },
  {
    "path": "/admin/companies/new",
    "permission": [
      "COMPANY_MANAGE"
    ]
  },
  {
    "path": "/admin/dashboard-manager",
    "permission": [
      "DASHBOARD_MANAGE"
    ]
  },
  {
    "path": "/admin/def/cities",
    "permission": [
      "GEOGRAPHY_MANAGE"
    ]
  },
  {
    "path": "/admin/def/company-types",
    "permission": [
      "COMPANYTYPE_MANAGE"
    ]
  },
  {
    "path": "/admin/def/component-types",
    "permission": [
      "DASHBOARD_MANAGE"
    ]
  },
  {
    "path": "/admin/def/countries",
    "permission": [
      "GEOGRAPHY_MANAGE"
    ]
  },
  {
    "path": "/admin/def/languages",
    "permission": [
      "LANGUAGE_MANAGE"
    ]
  },
  {
    "path": "/admin/def/menu-overrides",
    "permission": [
      "MENU_MANAGE"
    ]
  },
  {
    "path": "/admin/def/menu-toggles",
    "permission": [
      "MENU_MANAGE"
    ]
  },
  {
    "path": "/admin/def/menu-types",
    "permission": [
      "MENU_DESIGN"
    ]
  },
  {
    "path": "/admin/def/states",
    "permission": [
      "GEOGRAPHY_MANAGE"
    ]
  },
  {
    "path": "/admin/menu-by-type",
    "permission": []
  },
  {
    "path": "/admin/menu-designer",
    "permission": []
  },
  {
    "path": "/admin/menu-studio",
    "permission": [
      "MENU_DESIGN"
    ]
  },
  {
    "path": "/admin/menus",
    "permission": [
      "MENU_DESIGN"
    ]
  },
  {
    "path": "/admin/organization",
    "permission": [
      "TENANT_VIEW",
      "COMPANY_MANAGE",
      "UNIT_MANAGE"
    ]
  },
  {
    "path": "/admin/permission-matrix",
    "permission": [
      "SECURITY_MANAGE"
    ]
  },
  {
    "path": "/admin/permissions",
    "permission": [
      "ROLE_MANAGE"
    ]
  },
  {
    "path": "/admin/roles",
    "permission": [
      "ROLE_VIEW"
    ]
  },
  {
    "path": "/admin/roles/:id",
    "permission": [
      "ROLE_VIEW"
    ]
  },
  {
    "path": "/admin/security",
    "permission": [
      "SECURITY_MANAGE"
    ]
  },
  {
    "path": "/admin/system",
    "permission": []
  },
  {
    "path": "/admin/tenants",
    "permission": [
      "TENANT_MANAGE",
      "TENANT_VIEW"
    ]
  },
  {
    "path": "/admin/tenants/:id",
    "permission": [
      "TENANT_MANAGE",
      "TENANT_VIEW"
    ]
  },
  {
    "path": "/admin/tenants/new",
    "permission": [
      "TENANT_MANAGE"
    ]
  },
  {
    "path": "/admin/units",
    "permission": [
      "UNIT_MANAGE"
    ]
  },
  {
    "path": "/admin/users",
    "permission": [
      "USER_VIEW"
    ]
  },
  {
    "path": "/admin/users/:id",
    "permission": [
      "USER_VIEW"
    ]
  },
  {
    "path": "/admin/users/import",
    "permission": [
      "USER_MANAGE"
    ]
  },
  {
    "path": "/admin/users/new",
    "permission": [
      "USER_MANAGE"
    ]
  },
  {
    "path": "/dashboard",
    "permission": []
  },
  {
    "path": "/settings",
    "permission": []
  }
];

export const MENU_ROUTES: RouteEntry[] = ROUTES.filter(r => !r.path.includes(':'));

export const KNOWN_ROUTES: ReadonlySet<string> = new Set(ROUTES.map(r => r.path));
