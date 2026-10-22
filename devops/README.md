# devops

Shared local infrastructure for all backends.

| Service | Purpose | Port |
| --- | --- | --- |
| `mssql` | SQL Server 2022 (Developer) | `1435` → container `1433` |
| `mssql-init` | One-shot job that creates the `FSIDENTITY` database | — |
| `api-setup` | One-shot: applies the migrations and loads the reference data | — |
| `api` | The .NET API | `5240` |
| `web` | The web console, built (nginx; forwards `/api` to `api`) | `3041` |
| `web-dev` | The web console with hot reload (Vite dev server, source mounted from `frontends/web-react-vite`); only with `--profile dev` | `3040` |

## Usage

```bash
cd devops
cp .env.example .env     # set MSSQL_SA_PASSWORD
docker compose up -d
docker compose up -d --build   # needs JWT_KEY in .env as well
docker compose ps
# first user, on an empty database only:
docker compose run --rm --entrypoint "dotnet FSIdentity.Api.dll --seed-identity" api-setup
# or a demo (two customers, four applications, everybody's password Demo@12345):
docker compose run --rm --entrypoint "dotnet FSIdentity.Api.dll --seed-demo" api-setup
# then open http://localhost:3041
```

Hot reload while working on the web console:

```bash
docker compose --profile dev up -d web-dev   # then open http://localhost:3040; saving a file updates the tab
docker compose logs -f web-dev
```

`web` (port 3041) is the built image: after changing the web code, `docker compose up -d --build web` refreshes it.

Stop: `docker compose down` (data kept). Reset everything: `docker compose down -v`.

Never commit `.env`.
