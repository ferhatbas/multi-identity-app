import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';
import { AuthProvider } from '@/features/auth/AuthContext';
import { ProtectedRoute, RequirePermission, RequirePlatformAdmin } from '@/features/auth/guards';
import { P } from '@/features/auth/permissions';
import { I18nProvider } from '@/i18n';
import { ToastProvider } from '@/components/ui/Toast';
import { MainLayout } from '@/main-layout/MainLayout';
import { ThemeProvider } from '@/main-layout/ThemeContext';
import { AccountPage } from '@/pages/account/AccountPage';
import { RequiredStepPage } from '@/pages/account/RequiredStepPage';
import { AuditPage } from '@/pages/admin/AuditPage';
import { CompaniesListPage, CompanyCreatePage, CompanyEditorPage } from '@/pages/admin/companies/CompanyPages';
import { TenantCreatePage, TenantEditorPage, TenantsListPage } from '@/pages/admin/tenants/TenantPages';
import { RoleEditorPage } from '@/pages/admin/RoleEditorPage';
import { AccessStudioPage } from '@/pages/admin/access/AccessStudioPage';
import { PermissionMatrixPage, PermissionsPage } from '@/pages/admin/access/PermissionPages';
import { RolesListPage } from '@/pages/admin/roles/RolesListPage';
import { SecurityPage } from '@/pages/admin/SecurityPage';
import { SettingsPage } from '@/pages/admin/SettingsPage';
import { ApplicationTypesPage } from '@/pages/admin/applications/ApplicationTypesPage';
import {
  CitiesPage, CompanyTypesPage, ComponentTypesPage, CountriesPage, LanguagesPage, MenuOverridesPage, MenuTogglesPage, MenuTypesPage, StatesPage,
} from '@/pages/admin/definitions/DefinitionPages';
import { DashboardManagerPage } from '@/pages/admin/dashboard/DashboardManagerPage';
import { MenuStudioPage } from '@/pages/admin/menus/MenuStudioPage';
import { OrganizationPage } from '@/pages/admin/OrganizationPage';
import { MenusPage } from '@/pages/admin/menus/MenusPage';
import { SystemPage } from '@/pages/admin/SystemPage';
import { UnitsPage } from '@/pages/admin/UnitsPage';
import { UserCreateWizardPage } from '@/pages/admin/users/UserCreateWizardPage';
import { UserEditorPage } from '@/pages/admin/users/UserEditorPage';
import { UsersImportPage } from '@/pages/admin/users/UsersImportPage';
import { UsersListPage } from '@/pages/admin/users/UsersListPage';
import { ConfirmEmailPage, ForgotPasswordPage, ResetPasswordPage } from '@/pages/auth/RecoveryPages';
import { LoginPage } from '@/pages/auth/LoginPage';
import { DashboardPage } from '@/pages/DashboardPage';
import { NotFoundPage } from '@/pages/NotFoundPage';

export default function App() {
  return (
    <ThemeProvider>
      <I18nProvider>
        <ToastProvider>
          <AuthProvider>
            <BrowserRouter>
              <Routes>
                <Route path="/auth/login" element={<LoginPage />} />
                <Route path="/auth/forgot-password" element={<ForgotPasswordPage />} />
                <Route path="/auth/reset-password" element={<ResetPasswordPage />} />
                <Route path="/auth/confirm-email" element={<ConfirmEmailPage />} />

                <Route element={<ProtectedRoute />}>
                  <Route path="/required" element={<RequiredStepPage />} />
                  <Route element={<MainLayout />}>
                    <Route index element={<Navigate to="/dashboard" replace />} />
                    <Route path="/dashboard" element={<DashboardPage />} />
                    <Route path="/account" element={<AccountPage />} />
                    <Route path="/account/security" element={<AccountPage />} />
                    <Route path="/settings" element={<SettingsPage />} />
                    <Route path="/admin/users" element={<RequirePermission permission={P.UserView}><UsersListPage /></RequirePermission>} />
                    <Route path="/admin/users/new" element={<RequirePermission permission={P.UserManage}><UserCreateWizardPage /></RequirePermission>} />
                    <Route path="/admin/users/import" element={<RequirePermission permission={P.UserManage}><UsersImportPage /></RequirePermission>} />
                    <Route path="/admin/users/:id" element={<RequirePermission permission={P.UserView}><UserEditorPage /></RequirePermission>} />
                    <Route path="/admin/roles" element={<RequirePermission permission={P.RoleView}><RolesListPage /></RequirePermission>} />
                    <Route path="/admin/roles/:id" element={<RequirePermission permission={P.RoleView}><RoleEditorPage /></RequirePermission>} />
                    <Route path="/admin/access" element={<RequirePermission permission={P.RoleManage}><AccessStudioPage /></RequirePermission>} />
                    <Route path="/admin/permissions" element={<RequirePermission permission={P.RoleManage}><PermissionsPage /></RequirePermission>} />
                    <Route path="/admin/permission-matrix" element={<RequirePermission permission={P.SecurityManage}><PermissionMatrixPage /></RequirePermission>} />
                    <Route path="/admin/companies" element={<RequirePermission permission={[P.CompanyManage, P.TenantView]}><CompaniesListPage /></RequirePermission>} />
                    <Route path="/admin/companies/new" element={<RequirePermission permission={P.CompanyManage}><CompanyCreatePage /></RequirePermission>} />
                    <Route path="/admin/companies/:id" element={<RequirePermission permission={[P.CompanyManage, P.TenantView]}><CompanyEditorPage /></RequirePermission>} />
                    <Route path="/admin/units" element={<RequirePermission permission={P.UnitManage}><UnitsPage /></RequirePermission>} />
                    <Route path="/admin/tenants" element={<RequirePermission permission={[P.TenantManage, P.TenantView]}><TenantsListPage /></RequirePermission>} />
                    <Route path="/admin/tenants/new" element={<RequirePermission permission={P.TenantManage}><TenantCreatePage /></RequirePermission>} />
                    <Route path="/admin/tenants/:id" element={<RequirePermission permission={[P.TenantManage, P.TenantView]}><TenantEditorPage /></RequirePermission>} />
                    <Route path="/admin/system" element={<RequirePlatformAdmin><SystemPage /></RequirePlatformAdmin>} />
                    <Route path="/admin/application-types" element={<RequirePermission permission={P.ApplicationTypeManage}><ApplicationTypesPage /></RequirePermission>} />
                    <Route path="/admin/def/company-types" element={<RequirePermission permission={P.CompanyTypeManage}><CompanyTypesPage /></RequirePermission>} />
                    <Route path="/admin/def/component-types" element={<RequirePermission permission={P.DashboardManage}><ComponentTypesPage /></RequirePermission>} />
                    <Route path="/admin/def/countries" element={<RequirePermission permission={P.GeographyManage}><CountriesPage /></RequirePermission>} />
                    <Route path="/admin/def/states" element={<RequirePermission permission={P.GeographyManage}><StatesPage /></RequirePermission>} />
                    <Route path="/admin/def/cities" element={<RequirePermission permission={P.GeographyManage}><CitiesPage /></RequirePermission>} />
                    <Route path="/admin/def/languages" element={<RequirePermission permission={P.LanguageManage}><LanguagesPage /></RequirePermission>} />
                    <Route path="/admin/def/menu-types" element={<RequirePermission permission={P.MenuDesign}><MenuTypesPage /></RequirePermission>} />
                    <Route path="/admin/def/menu-toggles" element={<RequirePermission permission={P.MenuManage}><MenuTogglesPage /></RequirePermission>} />
                    <Route path="/admin/def/menu-overrides" element={<RequirePermission permission={P.MenuManage}><MenuOverridesPage /></RequirePermission>} />
                    <Route path="/admin/menus" element={<RequirePermission permission={P.MenuDesign}><MenusPage /></RequirePermission>} />
                    <Route path="/admin/menu-studio" element={<RequirePermission permission={P.MenuDesign}><MenuStudioPage /></RequirePermission>} />
                    <Route path="/admin/menu-designer" element={<Navigate to="/admin/menu-studio" replace />} />
                    <Route path="/admin/menu-by-type" element={<Navigate to="/admin/menu-studio" replace />} />
                    <Route path="/admin/dashboard-manager" element={<RequirePermission permission={P.DashboardManage}><DashboardManagerPage /></RequirePermission>} />
                    <Route path="/admin/organization" element={<RequirePermission permission={[P.TenantView, P.CompanyManage, P.UnitManage]}><OrganizationPage /></RequirePermission>} />
                    <Route path="/admin/security" element={<RequirePermission permission={P.SecurityManage}><SecurityPage /></RequirePermission>} />
                    <Route path="/admin/audit" element={<RequirePermission permission={P.AuditLogView}><AuditPage /></RequirePermission>} />
                    <Route path="*" element={<NotFoundPage />} />
                  </Route>
                </Route>
              </Routes>
            </BrowserRouter>
          </AuthProvider>
        </ToastProvider>
      </I18nProvider>
    </ThemeProvider>
  );
}
