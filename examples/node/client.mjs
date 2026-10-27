// A tiny client for the multi-identity-app contract. No dependencies, Node.js 20+.
// Local demo data only: the defaults are the published demo account.
const API_URL = process.env.API_URL ?? 'http://localhost:5240';
const API_USER = process.env.API_USER ?? 'admin';
const API_PASSWORD = process.env.API_PASSWORD ?? 'Demo@12345';

let auth = null;

async function call(path, { method = 'GET', body, token } = {}) {
  const res = await fetch(`${API_URL}${path}`, {
    method,
    headers: {
      ...(body ? { 'Content-Type': 'application/json' } : {}),
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
    body: body ? JSON.stringify(body) : undefined,
  });
  const envelope = await res.json();
  if (!res.ok) throw new Error(`${res.status} ${envelope.code}: ${envelope.message} (traceId ${envelope.traceId})`);
  return envelope.data;
}

async function signIn() {
  auth = await call('/api/Auth/login', { method: 'POST', body: { userName: API_USER, password: API_PASSWORD } });
  if (auth.twoFactorRequired) throw new Error('This account needs a two-factor code; use /api/Auth/login/2fa.');
}

async function refresh() {
  auth = await call('/api/Auth/refresh', { method: 'POST', body: { refreshToken: auth.refreshToken } });
}

// Calls the API; on 401 refreshes the token once and retries.
async function authed(path) {
  try {
    return await call(path, { token: auth.accessToken });
  } catch (e) {
    if (!String(e.message).startsWith('401')) throw e;
    await refresh();
    return call(path, { token: auth.accessToken });
  }
}

await signIn();

// Walk every page of /api/User/paged.
let page = 1;
for (;;) {
  const result = await authed(`/api/User/paged?PageNumber=${page}&PageSize=20`);
  for (const u of result.data) console.log(u.userName);
  if (page >= result.totalPages) break;
  page += 1;
}
