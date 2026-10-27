# Contributing

Thanks for helping. Issues and pull requests are welcome.

## Ground rules

- **The contract comes first.** A change to the API starts in [`contracts/`](contracts/). The OpenAPI snapshot test must stay green; `UPDATE_OPENAPI=1 dotnet test --filter OpenApiSnapshotTests` regenerates the snapshot on purpose.
- **Web screens follow one pattern**: list, then a tabbed `:id` editor with a sticky save bar, `/new` page or wizard for creation, modal for small definitions.
- **User-facing text is English.** The English sentence is the translation key; `npm run i18n` shows what a language file is missing.
- **No real secrets**: no passwords, keys or connection strings in code, docs or screenshots. Local settings live in `devops/.env` (git-ignored) or user-secrets.

## Run the checks before a pull request

```bash
dotnet test backends/dotnet/tests/FSIdentity.Tests
npm --prefix frontends/web-react-vite ci
npm --prefix frontends/web-react-vite run typecheck
npm --prefix frontends/web-react-vite run i18n
```

## Good first contributions

- A new backend (Python/FastAPI, Rust) that passes the same contract
- A new frontend against the same API
- A translation file for the web console
- Documentation and examples under [`examples/`](examples/)

## Pull requests

There is no gitflow: branch from `main`, open the pull request against `main`. Keep them focused, describe what changed and why, and mention how you tested it. Small PRs get reviewed faster.
