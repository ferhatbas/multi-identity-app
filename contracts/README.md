# contracts

What every backend in this repository (`backends/dotnet`, `backends/py`, `backends/rs`) must do the same way.

| File | What it is |
| --- | --- |
| [`api-envelope.md`](api-envelope.md) | The behavioural contract: response envelope, errors, paging, CRUD rules, tenancy, per-resource tables. Written for humans. |
| [`openapi.json`](openapi.json) | Machine-readable snapshot of the .NET API's surface: paths, methods, request and response schemas, and the `Bearer` security scheme. |

## How the two relate

`api-envelope.md` says **how the API behaves**. `openapi.json` says **what its surface looks like**. A backend is conformant when both hold: the same paths, methods and field names, and the same behaviour.

If the code and these files disagree, that is a bug in one of them. Fix it in the same change.

## Keeping `openapi.json` current

The .NET test `OpenApiSnapshotTests` fetches the live document and fails if it differs from the committed file, so the snapshot cannot silently drift. When you change the API on purpose:

```bash
# bash
UPDATE_OPENAPI=1 dotnet test --filter OpenApiSnapshotTests
```
```powershell
# PowerShell
$env:UPDATE_OPENAPI = "1"; dotnet test --filter OpenApiSnapshotTests; Remove-Item Env:UPDATE_OPENAPI
```

Run these from `backends/dotnet`, review the diff, commit it with the code change.

The `servers` entry is stripped from the snapshot because it holds the host the document was fetched from.

## Changing the contract

1. Change the behaviour and its tests in `backends/dotnet` (the reference implementation).
2. Update `api-envelope.md`.
3. Regenerate `openapi.json`.
