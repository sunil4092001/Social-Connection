// Shared helpers for the BuildConnect end-to-end tests. They call the API gateway exactly like the browser does.
// Every test registers its own users (e2e.*@example.com); scripts/../tests/e2e/cleanup.ps1 removes them afterwards.

export const GATEWAY = (process.env.E2E_GATEWAY ?? "http://localhost:5088") + "/api";
export const PASSWORD = "SecurePass123";
const runId = Date.now().toString(36);

export async function call(method, path, token, body) {
  const headers = { "Content-Type": "application/json" };
  if (token) headers.Authorization = `Bearer ${token}`;
  const response = await fetch(GATEWAY + path, { method, headers, body: body === undefined ? undefined : JSON.stringify(body) });
  const text = await response.text();
  let json = null;
  try { json = text ? JSON.parse(text) : null; } catch { /* not JSON */ }
  return { status: response.status, json };
}

export const sleep = (ms) => new Promise(resolve => setTimeout(resolve, ms));

/** A tiny assertion log: prints PASS/FAIL lines and keeps count. */
export function suite(title) {
  let passed = 0, failed = 0;
  console.log(`\n### ${title}`);
  return {
    section: (name) => console.log(`\n${name}`),
    check(name, ok, detail = "") {
      ok ? passed++ : failed++;
      console.log(`  ${ok ? "PASS" : "FAIL"} ${name}${ok ? "" : ` -> ${typeof detail === "string" ? detail : JSON.stringify(detail)}`}`);
    },
    finish() {
      console.log(`\n${title}: ${passed} passed, ${failed} failed`);
      return { passed, failed };
    }
  };
}

/** Waits until the gateway and the given API prefixes answer (any status below 500). */
export async function waitForStack(paths = ["/skills", "/auth/profile", "/posts/feed", "/connections/me", "/notifications", "/requirements"]) {
  for (let i = 0; i < 120; i++) {
    try {
      const responses = await Promise.all(paths.map(path => fetch(GATEWAY + path)));
      if (responses.every(r => r.status < 500)) return;
    } catch { /* not up yet */ }
    await sleep(1000);
  }
  throw new Error(`Services behind ${GATEWAY} did not become ready.`);
}

/** Registers and logs in a new user. Returns { id, token, email, name }. */
export async function newUser(label, profile) {
  const email = `e2e.${label}.${runId}.${Math.random().toString(36).slice(2, 7)}@example.com`;
  const firstName = profile.firstName ?? label;
  const registration = await call("POST", "/auth/register", null, { email, password: PASSWORD, lastName: "Tester", ...profile, firstName });
  if (registration.status !== 200) throw new Error(`Registering ${label} failed: ${registration.status} ${JSON.stringify(registration.json)}`);
  const login = await call("POST", "/auth/login", null, { email, password: PASSWORD });
  const token = login.json.data.accessToken;
  const id = JSON.parse(Buffer.from(token.split(".")[1], "base64url").toString()).sub;
  return { id, token, email, refreshToken: login.json.data.refreshToken, name: `${firstName} Tester` };
}

export const customer = (label, location = "Pune") => newUser(label, { userType: "CUSTOMER", location });
// ROOFER is used because no real data in the development databases uses it, so matching is predictable.
export const roofer = (label, location = "Mumbai") => newUser(label, { userType: "PROFESSIONAL", professionalType: "ROOFER", experienceYears: 5, location });

/** Polls the user's notifications until one matches. */
export async function waitForNotification(user, predicate, timeoutMs = 30000) {
  const start = Date.now();
  while (Date.now() - start < timeoutMs) {
    const page = await call("GET", "/notifications?page=1&pageSize=50", user.token);
    const found = (page.json?.data?.items ?? []).filter(predicate);
    if (found.length) return found;
    await sleep(500);
  }
  return [];
}

/** Makes two users connected (request + accept). */
export async function connect(from, to) {
  await call("POST", `/connections/request?addresseeUserId=${to.id}`, from.token);
  await call("POST", "/connections/accept", to.token, { requesterUserId: from.id });
}
