# API contract

Applies to every backend. The .NET implementation (`backends/dotnet`) is the reference; each rule below is covered by one of its tests.

## 1. Conventions

- **Base path:** `/api/<Resource>` (PascalCase resource name: `/api/Country`, `/api/Company`). Dropdown lookups live under `/api/geography`.
- **Format:** JSON, UTF-8, `Content-Type: application/json`. A request body of any other content type is refused with `415`.
- **Property names:** camelCase, including the keys of dictionaries.
- **Nulls:** always written (`"region": null`), never omitted.
- **Ids:** 64-bit integers. (JavaScript clients lose precision above 2^53; ids in this system stay far below.)
- **Enums:** numbers. The only enum on the wire is `types` (see 3).
- **Date-times:** ISO 8601, always **UTC with a trailing `Z`**, e.g. `2026-10-09T19:26:42.4039325Z`. The same value reads the same just after it was created and when read back later. Clients must accept 1 to 7 fractional digits.
- **Unknown request properties are ignored.** A client cannot write `id`, audit fields, `isDeleted` or an owner by sending them.

## 2. Request id

- Every response carries `X-Request-ID`, including errors and routing failures.
- A request may send its own `X-Request-ID`. It is accepted only if it is 1 to 64 characters from `A-Z a-z 0-9 - _ .`; otherwise it is replaced.
- Without one, the server generates 32 lowercase hex characters.
- The envelope's `traceId` always equals the response's `X-Request-ID`.
- The header is exposed to browsers through CORS (`Access-Control-Expose-Headers`).

## 3. The envelope

Every response body, success or error, is:

| Field | Type | Meaning |
| --- | --- | --- |
| `code` | string | The HTTP status as text: `"200"`, `"201"`, `"404"`... |
| `message` | string | Human-readable. Clients must not branch on its text. |
| `data` | any, or `null` | The payload. `null` for every error. |
| `types` | integer | `0` success, `1` client error (4xx), `2` server error (5xx), `3` reserved, unused. |
| `errors` | array | Empty on success. See 4. |
| `timestamp` | string | UTC, when the response was made. |
| `traceId` | string | The request id (see 2). |

Exactly these seven fields, nothing else.

```json
{
  "code": "200",
  "message": "Country retrieved successfully",
  "data": { "id": 1, "name": "Turkey", "code": "TR" },
  "types": 0,
  "errors": [],
  "timestamp": "2026-10-09T19:26:42.6433766Z",
  "traceId": "9c6a91aa8d574059b2c00824db4cc0f8"
}
```

## 4. Errors

**Every response with a status of 400 or more is in the envelope**, including the ones that never reach application code: an unknown address, a wrong HTTP method, a wrong content type, and (once sign-in exists) 401 and 403. There is no second error format.

Each entry of `errors`:

| Field | Type | |
| --- | --- | --- |
| `code` | string | Machine-readable, from the table below. Clients branch on this. |
| `message` | string | Human-readable. |
| `field` | string, or `null` | The JSON property that failed, in camelCase (`"formatCode"`, `"address.street"`). `""` when the problem is the whole body. `null` when no field applies. |
| `stackTrace` | `null` | Always `null`. Never filled. |
| `metadata` | `null` | Reserved. |

| Status | `errors[].code` | When |
| --- | --- | --- |
| 400 | `VALIDATION_ERROR` | The body failed validation (required, length, range, format, wrong JSON type, malformed JSON, empty body). One entry per failing field. |
| 400 | `BAD_REQUEST` | A rule about the request failed: invalid paging arguments, a referenced parent that does not exist ("Country 999 does not exist"), a tenant that is required but missing. One entry. |
| 401 | `UNAUTHORIZED` | Not signed in. *Reserved: sign-in is not implemented yet.* |
| 403 | `FORBIDDEN` | Signed in but not allowed (for example, creating a company in another tenant). |
| 404 | `NOT_FOUND` | No such route, or no such record **for this caller** (missing, soft-deleted, or in another tenant: never 403). |
| 405 | `METHOD_NOT_ALLOWED` | The path exists, the HTTP method does not. |
| 409 | `CONFLICT` | The change would break uniqueness or integrity (see 8). |
| 415 | `UNSUPPORTED_MEDIA_TYPE` | The request body is not JSON. |
| 422 | `VALIDATION_ERROR` | *Reserved* for business-level validation. Not used yet. |
| 500 | `INTERNAL_ERROR` | Unexpected failure. The message is always `An unexpected error occurred`; nothing about the cause is revealed. |

Malformed JSON, an empty body, `null` and a wrong-typed field each produce **one** error entry (never a second, redundant "body is required" entry):

```json
{ "code": "400", "message": "Invalid input data", "data": null, "types": 1,
  "errors": [ { "code": "VALIDATION_ERROR", "message": "The JSON value could not be converted to System.Int32. Path: $.displayOrder | LineNumber: 0 | BytePositionInLine: 51.",
                "field": "displayOrder", "stackTrace": null, "metadata": null } ],
  "timestamp": "2026-10-09T19:26:42.6Z", "traceId": "abc" }
```

## 5. Resource endpoints

Every resource below exposes the same eight operations. `<R>` is the resource name, `<Rs>` its plural in messages.

| Operation | Success | `data` |
| --- | --- | --- |
| `GET /api/<R>` | 200, `"<Rs> retrieved successfully"`, or `"No <Rs> found"` with an empty array (still 200) | array, ordered by id, **not paged** |
| `GET /api/<R>/paged` | 200 | a page (see 6) |
| `GET /api/<R>/deleted` | 200, `"<Rs> (deleted)"` | array of soft-deleted records |
| `GET /api/<R>/{id}` | 200 | the record |
| `POST /api/<R>` | **201** | the created record, with its new `id` |
| `PUT /api/<R>/{id}` | 200 | the updated record |
| `DELETE /api/<R>/{id}` | 200, `"<R> deleted successfully"` | `true` |
| `POST /api/<R>/{id}/restore` | 200, `"<R> restored"` | the restored record |

Rules:

- **`PUT` replaces the whole record.** An optional field left out is cleared to `null`. The one exception is `isActive`: left out, it keeps its value. Required fields must be present.
- **Delete is a soft delete.** The row stays; `isDeleted`, `deletedAt`, `deletedBy` are set. A soft-deleted record is invisible to `GET` (404) and listed only by `/deleted`.
- **Restore** clears `isDeleted`, `deletedAt`, `deletedBy`. Restoring a record that is not deleted is 404.
- **`{id}` must be an integer.** Anything else is a 404.
- **Every record** also carries `createdAt`, `createdBy`, `lastModifiedAt`, `lastModifiedBy`, `deletedAt`, `deletedBy`, `isDeleted`, `isActive`. They are set by the server only.
- Field names, types and lengths are in [`openapi.json`](openapi.json).

## 6. Paging, sorting, search

`GET /api/<R>/paged?pageNumber=&pageSize=&orderBy=&desc=&search=`

| Parameter | Default | Rule |
| --- | --- | --- |
| `pageNumber` | 1 | At least 1, else 400 `BAD_REQUEST`. |
| `pageSize` | 10 | 1 to 500, else 400 `BAD_REQUEST`. |
| `orderBy` | `id` | Only the names listed for the resource below, case-insensitive. **Any other name silently falls back to `id`**; it is never an error and never reaches the database. |
| `desc` | false | |
| `search` | none | Trimmed. Substring match on the fields listed below. |

- Rows with equal sort values are ordered by `id`, so pages never overlap or skip.
- A page beyond the last one is 200 with an empty `data`.
- `data` of a paged response: `{ "pageNumber", "pageSize", "totalPages", "totalRecords", "data": [...] }`. `totalPages` is `ceil(totalRecords / pageSize)`, `0` when empty.
- **Search is case-insensitive and accent-insensitive**, including the Turkish letters: `istanbul` finds `İstanbul`, `sanliurfa` finds `Şanlıurfa`, `agri` finds `Ağrı`, `ISPARTA` finds `isparta`. On SQL Server this is the collation `Latin1_General_100_CI_AI` on every text column. (`Turkish_*` collations are not used: they would stop `ISPARTA` matching `isparta`.)

| Resource | `orderBy` names | `search` fields |
| --- | --- | --- |
| Country | name, nameGlobal, code, region, currencyCode, isActive, createdAt | name, code, nameGlobal, region |
| State | code, name, countryId, isActive, createdAt | code, name, description |
| City | code, name, stateId, isActive, createdAt | code, name, description |
| Language | code, name, displayOrder, isActive | code, name, nativeName |
| Tenant | name, code, isActive, createdAt | name, code |
| CompanyType | name, code, isActive, createdAt | name, code, description |
| Company | name, code, city, isActive, createdAt | name, code, city, email |
| ApplicationType | name, code, displayOrder, isActive, createdAt | name, code, description |
| AppModule | code, name, displayOrder, isCore, isActive, createdAt | code, name, description |
| CompanyTypeApplicationType | companyTypeId, applicationTypeId, isDefault, isActive, createdAt | (none) |
| ApplicationTypeModule | applicationTypeId, appModuleId, isActive, createdAt | (none) |
| User | userName, email, firstName, lastName, createdAt, isActive | userName, email, firstName, lastName |
| Permission | name, code, isManagement, createdAt, isActive | name, code, description |
| Role | name, code, applicationTypeId, createdAt, isActive | name, code, description |
| RolePermission, UserCompany, UserCompanyRole, UserCompanyPermission | their foreign keys, createdAt, isActive | (none) |

## 7. Codes

Resources that have a `code` normalise it on write:

| Resource | Normalisation | Unique |
| --- | --- | --- |
| Country, State, City, Tenant, CompanyType, ApplicationType, AppModule | trimmed, **UPPER case** | among live (not soft-deleted) records |
| Language | trimmed, **lower case** (ISO 639-1) | among live records |
| Company | trimmed, UPPER case | among live records **of the same tenant** (two tenants may both have `HQ`) |

Two link resources have a pair instead of a code, unique among live records: `CompanyTypeApplicationType` by (`companyTypeId`, `applicationTypeId`) and `ApplicationTypeModule` by (`applicationTypeId`, `appModuleId`).

A taken code (or pair) is `409 CONFLICT`, whatever the letter case sent. A soft-deleted record's code is free again.

## 8. Integrity (soft delete cannot lean on the database)

| Operation | Refused with | When |
| --- | --- | --- |
| Create/update State | 400 | `countryId` does not exist or is soft-deleted |
| Create/update City | 400 | `stateId` does not exist or is soft-deleted |
| Create Company | 400 | `tenantId` does not exist (a tenant is never changed after creation) |
| Create/update Company | 400 | `countryId` or `companyTypeId` does not exist |
| Delete Country | 409 | it still has live states |
| Delete State | 409 | it still has live cities |
| Delete Tenant | 409 | it still has live companies, **in any tenant scope** |
| Delete CompanyType | 409 | a live company uses it, in any tenant |
| Create/update CompanyTypeApplicationType | 400 | `companyTypeId` or `applicationTypeId` does not exist or is soft-deleted |
| Create/update ApplicationTypeModule | 400 | `applicationTypeId` or `appModuleId` does not exist or is soft-deleted |
| Delete CompanyType | 409 | also: it still has application mappings |
| Delete ApplicationType | 409 | it is still mapped to a company type, or still grants a module |
| Delete AppModule | 409 | it is still granted to an application |
| Restore State / City / Company | 409 | its parent (country / state / tenant) is soft-deleted |
| Restore CompanyTypeApplicationType / ApplicationTypeModule | 409 | either parent is soft-deleted |
| Restore anything with a `code` | 409 | another live record has taken the code meanwhile |

## 9. Multi-tenancy

- Hierarchy: **Tenant → Company → User**. Everything a customer owns belongs to one tenant.
- The caller's identity comes from the token's claims (see 11): `TenantId`, `ActiveCompanyId`, `IsSuperAdmin`.
- **Company is tenant-scoped.** A caller with an active tenant sees, changes and deletes only that tenant's companies. A company of another tenant behaves as if it did not exist: `404`, never `403`. The same holds for `/paged`, `/deleted` and `/restore`.
- A caller with the `IsSuperAdmin` claim sees every tenant's companies: the claim lifts the tenant scope for Company. It never lifts soft delete.
- **Creating a company:** a caller with an active tenant always creates inside it (`tenantId` may be omitted or equal to it); naming another tenant is `403 FORBIDDEN`. A caller with no active tenant must send `tenantId`, else `400 BAD_REQUEST` ("tenantId is required when no tenant is active"). A company never changes tenant: `PUT` ignores `tenantId`.
- **Global reference data** has no owner and is visible to everyone: Country, State, City, Language, CompanyType, Tenant, ApplicationType, AppModule, CompanyTypeApplicationType, ApplicationTypeModule.
- Integrity checks (section 8) look across tenants, so a caller cannot delete something another tenant still uses just because they cannot see it.

## 10. Geography dropdowns

Read-only, only live **and active** rows, no paging.

| Request | Returns |
| --- | --- |
| `GET /api/geography/states?countryCode=TR` | states of that country, ordered by `code` |
| `GET /api/geography/states` | all active states |
| `GET /api/geography/cities?stateId=1` | cities of that state, ordered by `name` |
| `GET /api/geography/cities?countryCode=TR` | all cities of that country |
| `GET /api/geography/cities` | all active cities |

Each item: `{ "id", "code", "name", "parentId" }`. `parentId` is the country for a state and the state for a city. `countryCode` is case-insensitive. **An unknown country code returns an empty list, not everything.**

## 11. Languages

- At most one live language is the **fallback** (`isFallback`).
- Marking a language as the fallback (on create or on update) **moves** the flag: it is cleared on the previous one in the same change.
- The fallback language cannot be deleted, deactivated, or have its flag cleared (409); mark another language instead.
- A restored language never regains the fallback flag.

## 11b. Applications and modules

A request travels the chain `Company → CompanyType → ApplicationType → AppModule`.

- A company type may use several applications (`CompanyTypeApplicationType`). **At most one live mapping per company type is the default** (`isDefault`). Saving a mapping with `isDefault: true` clears the flag on the company type's other mappings in the same operation; a restored mapping never takes the flag back.
- An application grants modules (`ApplicationTypeModule`). A module with `isCore: true` is granted to every application without a row.
- **The home application of a company** is, first match wins: its remembered preference when the company type still maps it; the default mapping; the application whose code equals the company type's code; the first mapped application that is not `MANAGEMENT`; `MANAGEMENT` only when nothing else is mapped. A remembered `MANAGEMENT` is honoured only for a company whose type code is `MANAGEMENT`. Inactive or soft-deleted applications and mappings never count.
- **A session's application** is the token's `ActiveApplicationTypeId` when the company type still maps it (`MANAGEMENT` included), otherwise the home application.
- **Module gate.** An endpoint can require a module; the caller's session application must grant it (any of several named modules). Otherwise `403` with error code `MODULE_NOT_GRANTED`. A super administrator is **not** exempt: a module is product scope, not privilege. Anonymous endpoints are not gated. While no application grants any module (a fresh database) the gate is open; a caller whose application cannot be resolved passes unless strict mode (`Modules:StrictMode`) is on. Module sets are cached per application for `Modules:CacheSeconds` seconds (default 60, `0` = off).

`GET /api/ModuleScope/mine` returns the caller's scope in the usual envelope:

```json
{ "applicationTypeId": 3, "configured": true, "modules": ["BILLING", "CORE", "LAB"] }
```

`modules` is sorted, upper case. `configured: false` means no grants exist yet and the server is failing open: a client must not hide screens on that basis.

## 11c. Audit trail

Every saved change to a record leaves an append-only row, written in the **same transaction** as the change: both are kept or neither. Bulk machine work (the `--seed` run) is not recorded. Entities can opt out, and secret columns (password and token hashes, 2FA secrets) never appear in a row.

| `action` | When | `oldValues` / `newValues` / `changedFields` |
| --- | --- | --- |
| `Create` | a record was added | all columns after the change |
| `Update` | columns changed | only the changed columns, before and after; the server's own stamps (`createdAt`, `lastModifiedBy`, ...) are not counted; saving an unchanged value writes nothing |
| `Delete` | soft delete | `isDeleted` before and after |
| `Restore` | soft-deleted record brought back | `isDeleted` before and after |
| `Publish` | a batch saved under an operator's note | `newValues` = `{ "note", "rows" }` |

Each row also carries who (`userId`, `userName`), where from (`ipAddress`, `userAgent`), the tenant and company, a `correlationId` (the `X-Correlation-ID` request header, else the trace id; one request's rows share it) and `createdAt` (UTC).

Read-only endpoints; the trail cannot be written through the API:

| Endpoint | |
| --- | --- |
| `GET /api/AuditLog/paged` | newest first. Query: `pageNumber`, `pageSize` (1-500, default 25), `entityName`, `action`, `userId`, `from`, `to`, `search` (entity name, user name, entity id, changed fields). `to` given as a date without a time means that whole day |
| `GET /api/AuditLog/actions` | the distinct actions in the rows the caller can see, for a filter list |
| `GET /api/AuditLog/{id}` | `404` when absent or outside the caller's tenant |
| `GET /api/AuditLog/entity/{entityName}/{entityId}?max=200` | one record's history, newest first (`max` 1-1000) |

A caller with an active tenant sees only that tenant's rows (another tenant's row is `404`, never `403`); the `IsSuperAdmin` claim sees all. Who may read the trail at all is a permission (see 14).

## 11d. Accounts: users, roles, permissions

| Resource | Notes |
| --- | --- |
| Permission | global; `code` trimmed UPPER case, unique among live records; `isManagement` marks a permission that administers other people's access |
| Role | tenant-scoped (the creator's tenant is stamped); `name` unique **within a tenant** (two tenants may both have `Nurse`); `code` optional, UPPER case; `applicationTypeId` null = usable in every application |
| RolePermission | (`roleId`, `permissionId`) unique among live records |
| UserCompany | a user's membership of a company with a rank `membershipType`: `0` Reader (read-only), `1` Standard, `2` CompanyAdmin, `3` TenantAdmin, `4` PlatformAdmin; (`userId`, `companyId`) unique |
| UserCompanyRole | a role held inside one company; (`userId`, `companyId`, `roleId`) unique, so one role may be held in two companies |
| UserCompanyPermission | a permission granted (`isGranted: true`) or taken away (`false`, even when a role gives it) for one user in one company; (`userId`, `companyId`, `permissionId`) unique |

All follow the common resource rules (sections 5 to 8). Integrity: every `*Id` must exist (`400`); a role with permissions, a permission granted to roles, a company with members, an application used by a role cannot be deleted (`409`); user names and e-mails are unique across the whole platform, **across tenants**.

A user's password hash and 2FA secret never appear in any response or in the audit trail.

## 11e. Authentication

Bearer tokens: `Authorization: Bearer <accessToken>`. A request without a valid token to a protected endpoint is `401` in the envelope (`UNAUTHORIZED`).

| Endpoint | Auth | |
| --- | --- | --- |
| `POST /api/Auth/login` | none | `{ userName, password }` (`userName` may be the e-mail). `200` with `data` = `{ accessToken, refreshToken, tokenType: "Bearer", expiresAt, user }` |
| `POST /api/Auth/refresh` | none | `{ refreshToken }`. Returns a **new** pair; the old refresh token is dead |
| `POST /api/Auth/logout` | token | ends the session at once: its access token is refused from now on and its refresh token no longer works |
| `GET /api/Auth/me` | token | `{ user, modules }`: who is signed in and which modules the active application grants |
| `POST /api/Auth/change-company` | token | `{ companyId }`. Allowed for a member of that company, or a platform administrator for any company. Returns a new `accessToken` (no new refresh token) |
| `POST /api/Auth/change-application` | token | `{ applicationTypeId }`. The company's type must map it (`403` otherwise); a switch holds for the session. Returns a new `accessToken` |
| `PUT /api/Auth/me/language` | token | `{ languageCode }` (a live language, or null to clear). Returns `{ languageCode, effectiveLanguageCode }`. Works for readers; `user.effectiveLanguageCode` in every sign-in answer and `me` is the user's own language, else the company's, else the tenant's, else the fallback language, else `en` |
| `POST /api/Auth/change-password` | token | `{ currentPassword, newPassword }`. All the user's *other* sessions end |

`user` = `{ id, userName, email, firstName, lastName, languageCode, isPlatformAdmin, tenantId, activeCompanyId, activeApplicationTypeId, twoFactorEnabled, mustChangePassword, companies: [{ companyId, name, code, membershipType }] }`. A password hash, a 2FA secret or a token hash is never returned.

- **Failures look alike.** A wrong password, an unknown user and an inactive user all answer `401` "Invalid user name or password", and take the same time.
- **Lockout.** 5 failed attempts within 15 minutes lock the account for 15 minutes: `423 ACCOUNT_LOCKED`, even for the right password. A successful sign-in resets the count. (Configurable per tenant once security policies exist.)
- **Refresh rotation.** Each refresh token works once. Presenting a used one means it leaked: the whole session ends and `401` follows, for the thief and for the owner.
- **Sessions.** One per sign-in. Access tokens last 15 minutes, a session 14 days from its last refresh (configurable). The company and application of a session are carried across refreshes and re-checked against what the user may still do.
- **Password rules.** At least 8 characters, a letter and a digit.
- Every attempt (user name typed, success or failure and why, IP, agent) is recorded; passwords never are.

## 11f. Authorization

**Closed by default.** Every endpoint needs a valid token except `POST /api/Auth/login`, `POST /api/Auth/refresh`, `GET /api/Health`, `GET /api/Health/ready` and the OpenAPI document. A request to an *unknown* route without a token is also `401`, not `404`: nothing is revealed to someone who is not signed in.

| Status | `errors[0].code` | When |
| --- | --- | --- |
| `401` | `UNAUTHORIZED` | no token, a bad or expired token, or a session that has ended |
| `403` | `PERMISSION_DENIED` | signed in, but the permission the endpoint needs is missing |
| `403` | `PASSWORD_CHANGE_REQUIRED` | the user must set a new password first; only sign-out, who-am-I and change-password work |
| `403` | `MODULE_NOT_GRANTED` | the active application does not grant the module (11b) |
| `403` | `READ_ONLY` | the caller is a Reader (11j) |
| `403` | `FORBIDDEN` | any other refusal (including not outranking the user an action is about) (naming another tenant, an application the company's type does not allow) |
| `403` | `MFA_SETUP_REQUIRED`, `EMAIL_CONFIRMATION_REQUIRED` | a restricted session (11k) |
| `409` | `SOD_CONFLICT` | separation of duties (11k) |
| `423` | `ACCOUNT_LOCKED` | too many failed sign-ins (11e) |

**What a user may do** is looked up in the database on every request, so a change to a role or grant applies to the very next request:

> the permissions of the roles the user holds **in the active company** that apply to the active application (a role with no `applicationTypeId` applies everywhere)
> **plus** permissions granted to them directly in that company, **minus** permissions taken from them directly there.

A user who is not an active member of the active company has none. A platform administrator (`IsSuperAdmin`) has all. `GET /api/Auth/me` returns the list as `permissions`.

**Per resource.** Reading is open to every signed-in user unless noted; writing (`POST`, `PUT`, `DELETE`, restore) needs the permission below. The permission is checked **before** the body is looked at.

| Resource | Read | Write |
| --- | --- | --- |
| Country, State, City | any signed-in user | `GEOGRAPHY_MANAGE` |
| Language | any | `LANGUAGE_MANAGE` |
| Tenant | `TENANT_VIEW` | `TENANT_MANAGE` |
| Company | any (tenant-scoped) | `COMPANY_MANAGE` |
| CompanyType | any | `COMPANYTYPE_MANAGE` |
| ApplicationType, AppModule, CompanyTypeApplicationType, ApplicationTypeModule | any | `APPTYPE_MANAGE` |
| Permission | `ROLE_VIEW` | `PERMISSION_MANAGE` |
| Role, RolePermission | `ROLE_VIEW` | `ROLE_MANAGE` |
| User | `USER_VIEW` | `USER_MANAGE` |
| UserCompany, UserCompanyRole, UserCompanyPermission | `USER_VIEW` | `USER_MANAGE` |
| SecurityPolicy, SoDRule | `SECURITY_MANAGE` | `SECURITY_MANAGE` |
| MenuType, Menu, MenuLocalization, MenuCompanyType, menu layouts | `MENU_DESIGN` | `MENU_DESIGN` |
| MenuPermissionOverride, MenuFeatureToggle | `MENU_MANAGE` | `MENU_MANAGE` |
| UnitType, Unit, UserUnit | any signed-in user | `UNIT_MANAGE` |
| TenantSetting, CompanySetting | `SETTINGS_MANAGE` | `SETTINGS_MANAGE` |
| ComponentType, DashboardWidget, DashboardLayout | `DASHBOARD_MANAGE` (ComponentType: any) | `DASHBOARD_MANAGE` |
| AuditLog (read only) | `AUDIT_LOG_VIEW` | none |

## 11g. Users

`User` is a normal resource (sections 5 to 8): read `USER_VIEW`, write `USER_MANAGE`. Tenant-scoped: a tenant's administrator sees only that tenant's users (another tenant's user is `404`).

| Create field | |
| --- | --- |
| `userName`, `email` | trimmed, **lower case**; unique among live users **across all tenants** (`409`) |
| `firstName`, `lastName` | required |
| `phoneNumber`, `languageCode` | optional |
| `password` | required; the password rules (8+ characters, a letter, a digit); stored only as a BCrypt hash |
| `shouldChangePasswordOnNextLogin` | default `true` |
| `tenantId` | only for a caller with no active tenant (then required and must exist); a caller with a tenant always creates inside it, naming another is `403` |

`PUT` changes the profile (`userName`, `email`, names, phone, language, `isActive`); it **cannot** change the password or make anyone a platform administrator. A read never contains the password, the hash or the 2FA secret.

- **Switching a user off** (`isActive: false`) or **deleting** them ends all their sessions at once; restoring them does not bring those sessions back. Nobody can delete or deactivate their own account (`409`). A user with company memberships cannot be deleted (`409`).
- `GET /api/User/{id}/effective-permissions?companyId=` (`USER_VIEW`): `{ userId, companyId, applicationTypeId, granted: [{ code, name, via: ["role:Nurse", "direct"] }], denied: [codes] }`. `companyId` defaults to the user's active company.
- `POST /api/User/{id}/copy` (`USER_MANAGE`): `{ userName, email, firstName?, lastName?, password?, copyMemberships, copyRoles, copyPermissions }` (the three flags default `true`). `201` with `{ user, temporaryPassword }`; `temporaryPassword` is a generated password, returned **once**, only when none was given (the copy must then change it at first sign-in).
- `POST /api/User/import` (`USER_MANAGE`): `{ users: [{ userName, email, firstName, lastName, password?, companyId?, membershipType?, roleIds? }], tenantId?, atomic }`, 1 to 500 rows. `200` with `{ created, rolledBack, users: [{ id, userName, temporaryPassword }], failed: [{ row, userName, error }] }`. A bad row (duplicate name or e-mail, even inside the file; bad e-mail; weak password; unknown company or role) is reported with its row number (1-based) and the other rows still go in, unless `atomic` is `true`: then one bad row means nothing is imported (`created: 0`, `rolledBack: true`).
- `POST /api/Role/{id}/copy` (`ROLE_MANAGE`): `{ name, code? }`. `201` with the new role, which has the same application scope and the same permissions.

## 11h. Forgotten passwords, e-mail confirmation, two-factor

| Endpoint | Auth | |
| --- | --- | --- |
| `POST /api/Auth/forgot-password` | none | `{ email }`. **Always** `200` with the same message, whether or not the address belongs to an account. An e-mail with a link `…/auth/reset-password?token=…` is sent to a real, active user. At most one request a minute per user; a new request voids the older link |
| `POST /api/Auth/reset-password` | none | `{ token, newPassword }`. The link works once and for 60 minutes. Sets the password, ends **all** the user's sessions, lifts a lockout, and sends a "password changed" notice. A bad, used, replaced or expired link is always `400` "The link is invalid or has expired". A weak password is `400` and the link stays usable |
| `POST /api/Auth/send-confirmation` | token | e-mails a confirmation link for the user's address (`409` when it is already confirmed) |
| `POST /api/Auth/confirm-email` | none | `{ token }`. One use, 24 hours, and only while the user still has the address the link was sent to |
| `POST /api/Auth/2fa/setup` | token | `{ secret, otpAuthUri }`: a new seed for an authenticator app. Nothing is on yet (`409` when 2FA is already on) |
| `POST /api/Auth/2fa/enable` | token | `{ code }`: a first code from the app turns it on (`400` if wrong). Returns `{ codes }`: **ten recovery codes, shown once** |
| `POST /api/Auth/2fa/disable` | token | `{ password, code }` (`code` = app code or recovery code). Removes the seed and the codes |
| `POST /api/Auth/2fa/recovery-codes` | token | `{ password, code }`. Returns ten new codes; the old ones stop working |
| `POST /api/Auth/login/2fa` | none | `{ twoFactorToken, code }`. See below |

**Signing in with 2FA on.** `POST /api/Auth/login` with the right password answers `200` with `data` = `{ twoFactorRequired: true, twoFactorToken }` and **no** access or refresh token. The `twoFactorToken` lives five minutes and is good for nothing but `login/2fa`; it is not accepted as a bearer token. `login/2fa` then returns the usual `{ accessToken, refreshToken, user, … }`. A wrong code is `401` and counts towards the lockout like a wrong password.

- A code from the app works **once**: using the same code again, or an older one, is refused. One 30-second step either side of now is accepted, for clock drift.
- A recovery code works once; letter case, dashes and spaces are ignored (`ab12c-d34ef` = `AB12C D34EF`).
- The seed is stored encrypted, recovery codes and reset/confirmation tokens only as hashes. None of them appear in a response after the moment they are shown, or in the audit trail.

## 11i. Administration of accounts

| Endpoint | Permission | |
| --- | --- | --- |
| `POST /api/User/{id}/reset-password` | `USER_MANAGE` | `{ password }`: a generated password, returned **once**. The user must change it at the next sign-in; all their sessions end, a lockout is lifted, they get a "password changed" e-mail |
| `POST /api/User/{id}/unlock` | `USER_MANAGE` | lifts a lockout |
| `GET /api/User/{id}/sessions` | `SESSION_MANAGE` | the user's last 100 sessions: `{ id, companyId, applicationTypeId, ipAddress, userAgent, createdAt, lastSeenAt, expiresAt, revokedAt, revokedReason, impersonatorUserId, isOpen }` |
| `POST /api/User/{id}/sessions/revoke` | `SESSION_MANAGE` | signs the user out everywhere; `data` = how many sessions ended |
| `POST /api/User/{id}/sessions/{sessionId}/revoke` | `SESSION_MANAGE` | ends one (`404` if it is not that user's) |
| `GET /api/Auth/sessions` | token | the signed-in user's own sessions; `isCurrent` marks this one |
| `POST /api/Auth/sessions/{sessionId}/revoke` | token | ends one of one's own sessions (`404` for anyone else's) |
| `POST /api/Auth/logout-all` | token | ends every session of the signed-in user, this one included |
| `POST /api/Auth/impersonate` | `IMPERSONATE` | `{ userId }`. Acts as that user; see below |

The target of an administrator's action is looked up through the caller's tenant: another tenant's user is `404`. Only a platform administrator may reset, unlock, sign out or impersonate a **platform administrator** (`403`).

**Impersonation.** The result has the same shape as a sign-in but **no refresh token**; the session lasts as long as one access token. The token's claims are the target's, plus `ImpersonatorId` = the administrator. You cannot act as yourself or as a switched-off user. While acting, `change-password`, every `2fa` endpoint and `impersonate` itself are `403`. Every audit row written during the session has `userId` = the target and `impersonatedBy` = the administrator, and the start is an `Impersonate` event.

**Security events in the audit trail.** Besides row changes (11c), these leave an event on `entityName: "User"`: `AdminPasswordReset`, `Unlock`, `SessionsRevoked`, `Impersonate`, `PasswordChanged`, `PasswordReset`, `TwoFactorEnabled`, `TwoFactorDisabled`. Audit rows now also carry `impersonatedBy`.

## 11j. Rank, management permissions and guards

A membership has a **rank**: `0` Reader, `1` Standard, `2` CompanyAdmin, `3` TenantAdmin, `4` PlatformAdmin. A caller's rank is that of their membership of the company they are working in; a platform administrator (`IsSuperAdmin`) is `4`; with no membership, `0`. A person's own rank, for the rules below, is the highest they hold in any company.

- **Who may administer whom.** Changing, deleting, restoring, resetting the password of, unlocking, signing out, copying, impersonating a user, or changing their memberships, roles or personal permissions, needs the caller to **outrank** them (`403` otherwise). A platform administrator may manage everybody. People may edit their own profile, but not give themselves anything.
- **Giving a rank.** A membership may only be given at a rank **below** the giver's own. `membershipType: 4` (PlatformAdmin) is never a membership, for anybody (`403`): platform administrator is a flag on the user.
- **Management permissions come with the rank.** A permission with `isManagement: true` must name `managementRank` (`2` or `3`). A user holds it as soon as their membership rank is at least that, and cannot get it any other way: giving one through a role (`RolePermission`) or a personal grant (`UserCompanyPermission`) is `403`, and a row that slipped in is ignored. Their source in `effective-permissions` is `rank:CompanyAdmin`, `rank:TenantAdmin`. A non-management permission must not have a `managementRank` (`400`).
- **Reserved role names.** `Administrator`, `Admin`, `Superadmin`, `Superuser`, `Root`, `System`, `Owner`, `Platform/Tenant/Company admin(istrator)`, `Reader`, `Management user`, compared without case, spaces, dashes or underscores, cannot be created or renamed to by anyone but a platform administrator (`400`).
- **Readers are read-only.** A caller of rank `0` (including a user with no membership in the active company) may use `GET`, `HEAD`, `OPTIONS` and the endpoints about their own account (sign out, change company/application/password, sessions, two-factor, send confirmation); anything else is `403` with code `READ_ONLY`. A platform administrator is never a Reader.
- **After-create hooks.** A resource may declare code that runs right after a record is created through the API (a new tenant getting its starting data). The record and what the hooks add are saved in one transaction: if a hook fails, the record is not created either.

## 11k. Security policies and separation of duties

**Policy resources.** `SecurityPolicy` (read and write `SECURITY_MANAGE`): one live policy per tenant, plus at most one with no tenant, the platform's own. Every value is optional; leave it out to inherit it.

| Field | Range | Meaning |
| --- | --- | --- |
| `minPasswordLength` | 6 to 64 | |
| `passwordRequiresLetter`, `passwordRequiresDigit`, `passwordRequiresSymbol`, `passwordRequiresMixedCase` | | |
| `passwordMaxAgeDays` | 1 to 3650 | an older password must be changed at the next sign-in |
| `maxFailedAttempts`, `lockoutWindowMinutes`, `lockoutMinutes` | 1 to 100, 1 to 1440 | the lockout of 11e |
| `sessionDays` | 1 to 365 | how long a session lives from its last refresh |
| `requireMfaFromRank` | `1` to `4` | users of this rank and above must have two-factor |
| `requireConfirmedEmail` | | |

A tenant's administrator may create and change **their own tenant's** policy; only a platform administrator may touch the platform's own policy (omit `tenantId`) or name another tenant (`tenantId`, only when no tenant is active).

**What really applies** to a person is computed: the built-in defaults (8 characters, a letter and a digit, 5 failures in 15 minutes lock for 15 minutes, 14 days), **replaced** by the platform's policy where it sets a value, then **tightened** by the tenant's: a tenant can only raise the minimum length, switch requirements on, lower the failure limit, lengthen the lockout and its window, shorten the session or the password's life, require two-factor from a lower rank, or require a confirmed e-mail. It can never loosen what the platform requires. `GET /api/Security/effective-policy` returns it (any signed-in user); `GET /api/Security/status` returns it with what the caller must still do.

**Restricted sessions.** Signing in (and refreshing) is allowed, but the token carries a restriction until it is lifted, and only the account's own endpoints work (sign out, who am I, change password, two-factor, confirm e-mail, sessions, `Security/*`):

| Token claim | `errors[0].code` | Lifted by |
| --- | --- | --- |
| `MustChangePassword` | `PASSWORD_CHANGE_REQUIRED` | `change-password`, then sign in again or refresh |
| `MfaSetupRequired` | `MFA_SETUP_REQUIRED` | `2fa/setup` + `2fa/enable`, then refresh |
| `EmailConfirmationRequired` | `EMAIL_CONFIRMATION_REQUIRED` | `send-confirmation` + `confirm-email`, then refresh |

`user.mustChangePassword`, `user.mfaSetupRequired`, `user.emailConfirmationRequired` in the sign-in response say the same. An impersonating administrator is not held up by the person's restrictions.

**Separation of duties.** `SoDRule` (`SECURITY_MANAGE`): two different permissions (`permissionAId`, `permissionBId`) that one person must not hold together in one company, however they come: through one role, two roles (of any application, since people can switch), a personal grant, or their rank. A rule with no tenant binds every tenant; a tenant's own binds that tenant. The check runs where permissions are handed out and answers `409 SOD_CONFLICT` with a message naming both permissions and the rule:

- adding a permission to a role (also: if anyone who holds the role would then hold both),
- giving a person a role, or a personal grant (`isGranted: true`),
- restoring any of these.

A new or changed rule is refused (`409`) when a role already holds both permissions. A permission that is part of a rule cannot be deleted (`409`).

## 11l. Navigation (menus) and language

`GET /api/Navigation/mine` (any signed-in user) returns the menu **this person** gets: a tree of `{ id, code, title, path, icon, displayOrder, children }`. `path` is empty for a group. An entry is shown only when **all** of these hold:

1. it belongs to the person's active application, or to every application (`applicationTypeId` empty);
2. it is not hidden for their company type or, failing that, their application, by a layout (a company type's layout beats the application's, for both hiding and `displayOrder`);
3. it is not limited to other company types (an entry with `MenuCompanyType` rows is shown only to those types);
4. its `featureCode` is not switched off for their tenant (`MenuFeatureToggle`: the tenant's row, else the platform's, else on);
5. the application grants its `moduleCode` (11b);
6. the person has the permission it needs: the tenant's `MenuPermissionOverride` replaces the entry's own `permissionId` (an override with no permission means "none needed"); a platform administrator has all.

An entry whose parent is not shown is not shown. A group with nothing under it is not shown. Titles are in the person's language, else the entry's own `title`.

`GET /api/Navigation/tree` (`MENU_DESIGN`) returns the whole definition unfiltered, for a designer.

| Resource | Read | Write | Notes |
| --- | --- | --- | --- |
| MenuType, Menu, MenuLocalization, MenuCompanyType, MenuApplicationTypeLayout, MenuCompanyTypeLayout | `MENU_DESIGN` | `MENU_DESIGN` | the product's own definition: a tenant does not draw it. `Menu.code` upper case, unique; the parent must exist, an entry cannot be its own ancestor, and cannot be in a different application than its parent; an entry with children cannot be deleted. `MenuLocalization` is unique per (`menuId`, `languageCode`) |
| MenuPermissionOverride, MenuFeatureToggle | `MENU_MANAGE` | `MENU_MANAGE` | tenant-scoped: a tenant's administrator sets their own tenant's; with no active tenant it is the platform's default |

**Which language a person is spoken to in:** their own `languageCode`, else their company's, else their tenant's, else the platform's fallback language (`isFallback`), else `en`. A code that does not exist or is switched off is skipped, not honoured.

## 11m. Units, settings, dashboards

**Organisation units.** `UnitType` (global; read any signed-in user, write `UNIT_MANAGE`; `code` UPPER case) and `Unit` (read any signed-in user of the company, write `UNIT_MANAGE`). A unit **belongs to the company the caller is working in** (`companyId` in the response; without an active company, `400`), is visible only inside it, and its `code` (UPPER case) is unique within the company (two companies may both have `OPS`). Units form a tree via `parentId`: a unit cannot be its own parent or moved under one of its children (`400`) and cannot be deleted while it has children (`409`). A `managerUserId` must be a member of the company (`400`). `GET /api/Unit/tree` returns the company's units as nested `{ id, code, name, unitTypeId, managerUserId, children }`. `UserUnit` puts a company member in a unit (`UNIT_MANAGE`), unique per (`userId`, `unitId`); one unit per person can be `isPrimary` and marking another moves the flag.

**Settings.** Three layers, the more specific one wins: the tenant's (`TenantSetting`), the company's (`CompanySetting`), the person's own. `TenantSetting` and `CompanySetting` are resources read and written with `SETTINGS_MANAGE`; `key` matches `^[A-Za-z][A-Za-z0-9_.-]{0,63}$` and is unique within the tenant or company; `value` up to 4000 characters. A new tenant starts with `TIME_ZONE=UTC` and `DATE_FORMAT=yyyy-MM-dd`, a new company with `WEEK_START=Monday` (created in the same transaction as the record).

| Endpoint | |
| --- | --- |
| `GET /api/Settings/effective?keys=a,b` | any signed-in user: `[{ key, value, source }]`, `source` = `user`, `company` or `tenant`; keys with no value are left out |
| `GET /api/Settings/mine` | the person's own settings |
| `PUT /api/Settings/mine/{key}` | `{ value }`. At most 200 per person |
| `DELETE /api/Settings/mine/{key}` | `data` = whether one was removed |

A person's settings are theirs alone: no endpoint reads another person's. Settings are for configuration and preferences, not secrets.

**Dashboards.** `ComponentType` (global, `code` UPPER) and `DashboardWidget` (the product's catalogue: `code` UPPER, `title`, optional `componentTypeId`, `permissionId`, `moduleCode`, `defaultWidth` 1 to 24, `defaultHeight`), both `DASHBOARD_MANAGE`. `DashboardLayout` (`DASHBOARD_MANAGE`): `{ name, scope, scopeId, layoutJson }` with `scope` `0` Application, `1` CompanyType, `2` Company, `3` User, `scopeId` the id of that thing (must exist), and `layoutJson` = `[{ "widget": "SALES", "x": 0, "y": 0, "w": 4, "h": 3 }, ...]` on a **24-column grid**: `x >= 0`, `w >= 1`, `x + w <= 24`, `h >= 1`, a widget at most once, every widget existing, at most 50. One layout per (tenant, scope, `scopeId`).

`GET /api/Dashboard/mine` (any signed-in user) returns `{ source, layoutId, items: [{ widget, title, componentType, x, y, w, h }] }`. The layout used is the most specific that exists, **user, then company, then company type, then application** (a tenant's own layout beats the platform's for the same scope); `source` says which (`user`, `company`, `companyType`, `application`, `default`). Whatever the layout says, a widget the person may not see (its permission, its module) is left out. With no layout, every widget they may see is laid out left to right. `PUT /api/Dashboard/mine` `{ items: [{ widget, x, y, w, h }] }` saves the caller's own layout (same rules, `400` otherwise); `DELETE /api/Dashboard/mine` goes back to the inherited one.

## 11n. Notifications (push)

Everything here is the signed-in person's **own**: no endpoint reads or changes another person's preferences, devices or history.

**Preferences.** Notifications have a `category` (`SECURITY`, `SYSTEM`, `ACTIVITY`, `MARKETING`) and a `channel` (`PUSH`, `EMAIL`). With no choice made, everything is on **except `MARKETING`**. `SECURITY` is mandatory: it is always on, `enabled: false` for it is `400`, and it is delivered even if a stored row says otherwise.

| Endpoint | |
| --- | --- |
| `GET /api/Notification/preferences` | every category x channel: `[{ category, channel, enabled, mandatory, isDefault }]` |
| `PUT /api/Notification/preferences` | `{ items: [{ category, channel, enabled }] }` (case-insensitive); unknown category or channel is `400`; returns the full matrix |

**Devices.** `POST /api/Notification/push-tokens` `{ token, platform, deviceName? }` registers the device (`platform` is `ios`, `android` or `web`; token 8 to 1024 characters). Registering a token already known is a check-in (same `id`); a token known under **another person moves to the caller**, because a device belongs to whoever signed in on it last. At most 20 devices per person (`400`). The token is a credential for sending to the device and is **never returned or audited**; tokens are compared case-sensitively. Closed while impersonating (`403`). `GET /api/Notification/push-tokens` lists `{ id, platform, deviceName, lastSeenAt, createdAt }`; `DELETE /api/Notification/push-tokens/{id}` returns whether one of the caller's was removed.

**Pushes.** `POST /api/Notification/push-test` `{ category, title, body }` pushes to the caller's own devices and returns one ticket per attempt. `GET /api/Notification/push-tickets` lists the last 50: `{ id, category, title, body, status, error, sentAt, createdAt }`, `status` = `Sent`, `Failed` or `Skipped`. A push **not sent says why** (`Skipped`: "Turned off by the user" or "No registered device"); a provider error is recorded as `Failed` and never fails the request. Inside the server, other features send through `IPushNotifier.NotifyAsync(userId, category, title, body)` and the provider sits behind `IPushSender` (the default only logs).

## 12. Token format and claims

Every backend issues and accepts the **same** access token, so a token from one is valid on the others. It is a JWT:

| | |
| --- | --- |
| Algorithm | `HS256`, signed with the shared secret (`Jwt:Key`, at least 32 characters, UTF-8 bytes) |
| `iss` / `aud` | `Jwt:Issuer` / `Jwt:Audience`, both `FSIdentity` by default; checked on every request |
| `exp` | issued at sign-in plus `Jwt:AccessMinutes` (15); a clock skew of 30 seconds is tolerated |
| `sub`, `jti` | the user id; a random id per token |
| Refresh token | opaque and random, **not** a JWT; only its hash is stored; rotated at every use (section 11e) |
| Two-factor challenge | a short JWT (5 minutes) with audience `<Audience>.2fa` and only `UserId`; it never works as an access token |

A token's claims are read by these exact names (a claim with no value, such as `TenantId` for a platform administrator, is left out):

| Claim | Meaning |
| --- | --- |
| `UserId` | The signed-in user's id. Written to `createdBy` / `lastModifiedBy` / `deletedBy`. |
| `TenantId` | The active tenant. |
| `ActiveCompanyId` | The active company. |
| `ActiveApplicationTypeId` | The active application. |
| `IsSuperAdmin` | `"true"` lifts the tenant and company scope for non-strict resources. It never lifts soft delete. |
| `UserName` | The user name, for the audit trail. |
| `sid` | The session. A token whose session has ended is refused: the session is checked in the database on every request. |
| `MustChangePassword` | `"true"` while the user has to set a new password first. Absent otherwise. |
| `MfaSetupRequired` | `"true"` while the user's rank requires two-factor and they have not set it up. Absent otherwise. |
| `EmailConfirmationRequired` | `"true"` while the tenant requires a confirmed e-mail address and the user's is not. Absent otherwise. |
| `ImpersonatorId` | The administrator acting as the user, during an impersonation. Absent otherwise. |

A token carrying `MustChangePassword`, `MfaSetupRequired` or `EmailConfirmationRequired` may only call the endpoints that resolve it (section 11k). Permissions are **not** in the token: they are looked up from the database at every request.

A token issued by any backend must be accepted by the others. A backend proves it by signing in through each of the others and calling `GET /api/Auth/me` with the result.

## 13. Reference data (`--seed`)

Loading the seed (idempotent, by code, safe to repeat) leaves a database with exactly:

| What | Count | Notes |
| --- | --- | --- |
| Languages | 4 | `en` (fallback), `tr`, `de`, `ar` (right-to-left) |
| Countries | 10 | TR, US, GB, DE, FR, ES, IT, NL, AZ, SA |
| States | 9 | Turkey's 7 regions (`TR-MAR`, `TR-EGE`, `TR-AKD`, `TR-ICA`, `TR-KAR`, `TR-DAN`, `TR-GDA`) and `US-CA`, `US-NY` |
| Cities | 83 | Turkey's 81 provinces with codes `TR-01` to `TR-81` (the plate number), and two US cities |

The seed also loads (idempotent, by code):

| What | Notes |
| --- | --- |
| ApplicationType `MANAGEMENT` | the vendor back office |
| AppModule `CORE` (`isCore`), `ADMIN` | |
| ApplicationTypeModule | `MANAGEMENT` grants `ADMIN` |

Re-running corrects changed rows in place, replaces a soft-deleted row by a new live one, and never moves a fallback language an administrator chose.

### 13b. First setup (`--seed-identity`)

For an **empty** database (or a test one) only: it refuses to run, changing nothing, when any user exists. All or nothing (one transaction), and it writes no audit rows. Settings come from the `IdentitySeed` section (`TenantCode` `DEFAULT`, `CompanyCode` `HQ`, `AdminUserName` `admin`, `AdminEmail`, `LanguageCode` `en`, `AdminPassword`). Without `AdminPassword` one is generated and printed once. Either way the administrator **must change it at first sign-in**; never commit a real one (use the `IdentitySeed__AdminPassword` environment variable).

| What | Notes |
| --- | --- |
| Reference data of 13 | the `MANAGEMENT` application and its modules (the same step as `--seed`) |
| CompanyType `MANAGEMENT` | mapped to the `MANAGEMENT` application as its default |
| Tenant, Company | the vendor's own, with the baseline settings of 11m |
| Permissions | every code the product declares. `USER_MANAGE` and `SESSION_MANAGE` are management permissions of rank `CompanyAdmin`; `ROLE_MANAGE`, `SECURITY_MANAGE` and `IMPERSONATE` of rank `TenantAdmin`; the rest are ordinary |
| Roles | `Administrator` (the tenant-level ordinary permissions: view tenant, users, roles and audit trail, manage companies, menu, dashboards, units, settings) and `Viewer` (view tenant, users, roles). Platform-level permissions (geography, languages, tenants, company types, application types, permissions, menu design) are in no role: a platform administrator holds them |
| Administrator | `IsPlatformAdmin`, member of the company as `TenantAdmin` with the `Administrator` role, email confirmed |
| Menu `MAIN` | `DASHBOARD`, `USERS`, `ROLES`, `COMPANIES`, `UNITS`, `TENANTS`, `SECURITY`, `AUDIT`, `SETTINGS`, each with its permission and module, English titles and Turkish translations |
| Dashboard | component type `CARD`; widgets `WELCOME` and `RECENT_ACTIVITY` (needs the audit permission) |

### 13c. Demo data (`--seed-demo`)

For an **empty** database only, like 13b: it refuses, changing nothing, when any user exists; all or nothing; no audit rows. It also loads the reference data of 13, makes **English the fallback language**, and gives every tenant and person the language `en`. What it creates comes from a *demo definition*, so another demo is another JSON file, not another build (`DemoSeed__File=path`; the built-in one is written out as `backends/dotnet/demo-seed.example.json`). Settings (`DemoSeed` section): `Password` (default `Demo@12345`, **everybody's**, never for real data), `MustChangePassword` (default false, so a demo works at once), `EmailDomain` (`example.test`), `LanguageCode` (`en`), `File`.

Built-in demo:

| What | Notes |
| --- | --- |
| Management | tenant `MANAGEMENT`, company `MANAGEMENT` (company type `MANAGEMENT`, application `MANAGEMENT`); `admin` is the platform administrator |
| Applications | `RETAIL`, `HOSPITALITY`, `LOGISTICS`, `EDUCATION`, each granting `ADMIN`; each also a company type of the same code that maps to it as the default |
| Tenants | `ALPHA` (companies `ALPHA-RETAIL`, `ALPHA-HOSPITALITY`) and `BETA` (`BETA-LOGISTICS`, `BETA-EDUCATION`) |
| Roles, per tenant | Shared: `Administrator`, `Manager` (people, roles, audit trail, units, settings), `Viewer`, `Staff` (no administration). Per application: the roles the definition gives each application (`Cashier` in Retail, another `Cashier` in Hospitality with other permissions, `Receptionist`, `Dispatcher`, `Teacher`), created in every tenant that uses the application. A role's name is unique per tenant **and application** |
| Tenant administrator | `alpha.admin`, `beta.admin`: rank `TenantAdmin` in every company of the tenant, role `Administrator` |
| Company administrator | `<company code>.admin`, e.g. `alpha-retail.admin`: rank `CompanyAdmin`, role `Manager` |
| Users | `<company code>.user1`, `.user2`: rank `Standard`, role `Staff` |
| Menu | one for every application; each person sees what their permissions and modules allow |

A definition lists the management tenant, the applications and the tenants with their companies, a company naming its application by code and its people by user name and name. It is checked before anything is written; every problem is reported at once (unknown application, a repeated tenant, company or user name, a tenant with no administrator or no company).

## 14. Not part of the contract yet

Rate limiting (`429`).
