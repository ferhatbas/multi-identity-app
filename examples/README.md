# Examples

Minimal clients that talk to a running API. They show the contract in use: sign in, keep the token, call a paged list, read the standard envelope. Start the stack first (see the root [README](../README.md): steps 1 to 4, demo data loaded).

| Example | What it shows | Needs |
| --- | --- | --- |
| [`curl/login-and-list.sh`](curl/login-and-list.sh) | Sign in and list users with plain `curl` | `curl`, `jq` |
| [`node/client.mjs`](node/client.mjs) | A tiny client that signs in, refreshes the token when it expires and walks every page | Node.js 20+, no packages |

Both read the address from `API_URL` (default `http://localhost:5240`), and the credentials from `API_USER` and `API_PASSWORD` (default: the local demo account `admin` and its published demo password).

```bash
bash examples/curl/login-and-list.sh
node examples/node/client.mjs
```

The same code works against every backend that follows the [contract](../contracts/api-envelope.md); that is the point of the project.
