# backends/dotnet

The .NET 10 implementation: ASP.NET Core controllers, EF Core 10, SQL Server 2022.

## Layout

```
src/
├── FSIdentity.Domain/    # base entities, generic CRUD layer, audit trail, security primitives, repository, unit of work, events
├── FSIdentity.Identity/  # the resources and services (geography, organization, accounts, authentication, authorization,
│                         #   security policies, menus, workspace, notifications), seeds, DbContext, migrations
└── FSIdentity.Api/       # hosting, middleware, startup wiring
tests/FSIdentity.Tests/   # unit and API tests (SQLite in memory)
```

Dependencies point one way: Api → Identity → Domain.

## Run

1. Start SQL Server: `cd ../../devops && docker compose up -d`
2. Store the connection string and the token signing key outside git:

   ```bash
   cd src/FSIdentity.Api
   dotnet user-secrets set "ConnectionStrings:Default" "Server=localhost,1435;Database=FSIDENTITY;User Id=sa;Password=<MSSQL_SA_PASSWORD>;TrustServerCertificate=True"
   dotnet user-secrets set "Jwt:Key" "<at least 32 random characters>"
   ```

3. Create the schema and load the reference data (languages, countries, Turkey's provinces, the MANAGEMENT application):

   ```bash
   dotnet run -- --migrate     # creates the database if needed and applies the migrations
   dotnet run -- --seed
   ```

4. **First setup**, for an empty database only: the first tenant and company, every permission, two roles, an administrator, the menu and the dashboard widgets.

   ```bash
   dotnet run -- --seed-identity
   ```

   Without `IdentitySeed__AdminPassword` set, a password is generated and printed once; the administrator must change it at first sign-in. It refuses to run when any user exists. See section 13b of [`contracts/api-envelope.md`](../../contracts/api-envelope.md).

   **Or a demo database** instead (also empty databases only): the management tenant, two customers with two companies each, four applications, and a tenant administrator, company administrators and users for each, all with the password `Demo@12345`:

   ```bash
   dotnet run -- --seed-demo
   ```

   The accounts are listed when it finishes. Change the password, names, companies or applications with `DemoSeed__Password` or a JSON file (`DemoSeed__File=demo.json`, start from [`demo-seed.example.json`](demo-seed.example.json)). Section 13c of the contract describes it.

5. Start the API: `dotnet run` (http://localhost:5231). The web console in [`frontends/web-react-vite`](../../frontends/web-react-vite) signs in against it. In Development the OpenAPI document is at `/openapi/v1.json` and a Swagger UI page at `/swagger` (it loads its script from a CDN). Sign in with `POST /api/Auth/login`, then press **Authorize** and paste the `accessToken`.

Everything except sign-in, token refresh, password reset, health and (in Development) the OpenAPI document needs a signed-in user: a new endpoint is closed unless it says `[AllowAnonymous]`.

## Configuration

Set through `appsettings*.json`, user-secrets, or environment variables (`Jwt__Key`). Real secrets never go in git.

| Section | Keys | Notes |
| --- | --- | --- |
| `ConnectionStrings:Default` | | SQL Server |
| `Jwt` | `Key` (required, 32+ characters), `Issuer`, `Audience`, `AccessMinutes` (15), `RefreshDays` (14) | Shared by every backend: see section 12 of the contract |
| `Login` | `MaxFailedAttempts` (5), `WindowMinutes` (15), `LockoutMinutes` (15) | Platform defaults; a tenant can only tighten them (security policies) |
| `Passwords` | `WorkFactor` (11), `MinLength` (8) | BCrypt cost |
| `Security` | `DataKey` | Encrypts two-factor seeds in the database; falls back to the signing key |
| `Email` | `Mode` (`Log` or `Smtp`), `From`, `SmtpHost`, `SmtpPort`, `SmtpUser`, `SmtpPassword`, `UseSsl`, `PublicUrl` | `Log` writes mails to the log, for development |
| `Modules` | `StrictMode`, `CacheSeconds` | Which parts of the product an application grants |
| `Cors:AllowedOrigins` | | The web client |
| `DemoSeed` | `Password`, `MustChangePassword`, `EmailDomain`, `LanguageCode`, `File` | Read only by `--seed-demo` |
| `IdentitySeed` | `TenantCode`, `CompanyCode`, `AdminUserName`, `AdminEmail`, `AdminPassword`, `LanguageCode` | Read only by `--seed-identity` |

## Docker

```bash
docker build -t fsidentity-api .
docker run -p 8080:8080 -e ConnectionStrings__Default="..." -e Jwt__Key="<32+ characters>" fsidentity-api
```

The image does not create the schema on its own: run it once with `--migrate` (`docker run ... fsidentity-api --migrate`), then start it normally. `/api/Health` answers while the process lives, `/api/Health/ready` only when the database answers.

## Test

```bash
dotnet test
```

`OpenApiSnapshotTests` compares the live API surface with `../../contracts/openapi.json`.
If you changed the API on purpose, see [`contracts/README.md`](../../contracts/README.md).

## Adding a resource

A resource is an entity, three DTOs and a two-line controller. Mapping, sort and search
allow-lists and integrity rules come from attributes (`[Sortable]`, `[Searchable]`, `[Upper]`,
`[Unique]`, `[Parent]`); extra business rules go in a `CrudRules<...>` class. See
`src/FSIdentity.Identity/Geography/Country*.cs`.
