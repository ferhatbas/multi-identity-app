# frontends/web-react-vite

The web console for the identity API: sign in, then manage the people, roles, companies and tenants the backend owns.
React 19, TypeScript, Vite, React Router and **PrimeReact 10** (DataTable, Dialog, Sidebar drawer, Dropdown, Menu, TabMenu,
Toast...). The look is a dense, neutral workspace with one accent colour: a header with the company, language, theme and
user menus, the server's menu on the left, tables with server-side paging and sorting, record editing in a right-hand drawer.
Light and dark themes.

It talks to **any backend** in this repository, because they share one contract
([`contracts/api-envelope.md`](../../contracts/api-envelope.md)). Code is organised by feature
(`features/<area>/<area>.service.ts`, `main-layout`, `pages`, route guards).

## Run

1. Start a backend (see [`backends/dotnet/README.md`](../../backends/dotnet/README.md)); it listens on http://localhost:5231.
2. ```bash
   cd frontends/web-react-vite
   npm install
   npm run dev
   ```
   Open http://localhost:3040 and sign in (the first user comes from `dotnet run -- --seed-identity`).

In Docker: `cd devops && docker compose --profile dev up -d web-dev` runs the same dev server in a container (hot reload through a bind mount, API at `api:8080`) on http://localhost:3040. The built image on port 3041 only changes with `docker compose up -d --build web`.

The dev server forwards `/api` to the backend, so the browser sees one origin and CORS is not involved. Another backend
address: `API_TARGET=http://localhost:5233 npm run dev`. For a production build behind a reverse proxy that serves `/api`
on the same host, `npm run build` is enough; to call an API on another host set `VITE_API_BASE_URL` and allow the origin
in the backend's `Cors:AllowedOrigins`.

## What is in it

| Area | Screens |
| --- | --- |
| Sign-in | Login, two-factor code, forgot and reset password, e-mail confirmation, the one-step screen for an account that must change its password / set up two-factor / confirm its e-mail |
| Shell | Menu **from the server** (`/api/Navigation/mine`: what you may open is what you see), company switcher, language (English, Türkçe), light and dark theme, an "acting as" banner during impersonation |
| Dashboard | The layout the server picks for the person; widgets `WELCOME` and `RECENT_ACTIVITY` |
| Administration | Users (with company membership, roles, effective permissions, sessions, reset password, unlock, act as), Roles (and their permissions), Permissions (read-only), Companies, Tenants, Security policies, Audit trail (with before/after view) |
| Account | Profile, change password, two-factor (QR code, recovery codes), own sessions, notification preferences, personal / company / tenant settings |

Every list is paged, sortable and searchable on the server and can be exported as CSV. Create and edit always open in a
centred dialog.

## Look and theme

PrimeReact's colours come from one stylesheet per theme, `public/themes/fs-light.css` and `fs-dark.css`. They are generated
from PrimeReact's Lara pink themes by `npm run theme` (`scripts/make-theme.mjs`: the pink palette becomes the accent
`#E11D74`, the greys become the console's neutrals, the font becomes Hanken Grotesk). To change the accent, edit the colour
table in that script and run it again. `src/styles/app.css` holds the tokens, the shell (header, side menu) and the density
tuning of PrimeReact's controls. `ThemeContext` swaps the stylesheet when the theme changes.

## How it is built

- `src/services/http/httpService.ts`: one `fetch` wrapper. Adds the bearer token, refreshes it shortly before it expires,
  and on a `401` refreshes once and replays the call (concurrent calls share one refresh, because the refresh token
  rotates). Errors become `ApiException` carrying the server's message and per-field errors.
- `src/services/http/crud.ts`: the eight operations every resource has, so a new resource is one line.
- `src/components/ui`: thin wrappers over PrimeReact that keep the screens simple: `DataTable` (lazy mode: the server pages, sorts and searches), `Modal` (a `Dialog`, or a right-hand `Sidebar` drawer), `Form` (`InputText`, `Dropdown`, `Checkbox`, `Tag`), `Toast`, `TabBar` (`TabMenu`).
- `src/components/ui/ResourcePage.tsx`: a whole list screen (search, sort, page, export, create/edit/delete dialog) from
  column and field definitions. Tenants, companies, roles, security policies and settings are made with it.
- `src/features/auth`: token storage, `AuthContext` (`can(permission)` reads the permissions `/api/Auth/me` returns),
  route guards. The server enforces every permission; the guards only spare people pages that would answer `403`.
- `src/i18n`: the English sentence is the key; `tr.ts` holds the Turkish. A missing key shows the English.

## Checks

```bash
npm run typecheck   # TypeScript
npm run i18n        # translation files against the code
npm run build       # production build
```

There are no automated browser tests yet.
