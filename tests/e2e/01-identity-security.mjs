// Registration, login, refresh-token rotation and theft detection, logout, and protected endpoints.
import crypto from "node:crypto";
import { call, customer, suite, PASSWORD } from "./lib.mjs";

export default async function run() {
  const t = suite("Identity and security");
  const user = await customer("sec-a");

  t.section("Registration and login");
  t.check("duplicate email (other case) is a conflict (409)", (await call("POST", "/auth/register", null, { email: user.email.toUpperCase(), password: PASSWORD, firstName: "X", lastName: "Y", userType: "CUSTOMER" })).status === 409);
  t.check("invalid registration is rejected (400)", (await call("POST", "/auth/register", null, { email: "not-an-email", password: "x" })).status === 400);
  t.check("wrong password (401)", (await call("POST", "/auth/login", null, { email: user.email, password: "WrongPass999" })).status === 401);
  t.check("access token works (GET /users/me 200)", (await call("GET", "/users/me", user.token)).status === 200);
  const payload = JSON.parse(Buffer.from(user.token.split(".")[1], "base64url").toString());
  t.check("access token lives at most 15 minutes", payload.exp - Math.floor(Date.now() / 1000) <= 15 * 60 + 5);
  const h = Buffer.from(JSON.stringify({ alg: "HS256", typ: "JWT" })).toString("base64url");
  const p = Buffer.from(JSON.stringify({ ...payload, exp: Math.floor(Date.now() / 1000) + 600 })).toString("base64url");
  const forged = `${h}.${p}.${crypto.createHmac("sha256", crypto.randomBytes(48)).update(`${h}.${p}`).digest("base64url")}`;
  t.check("token signed with another key is rejected (401)", (await call("GET", "/users/me", forged)).status === 401);

  t.section("Refresh tokens");
  const rotated = await call("POST", "/auth/refresh-token", null, { refreshToken: user.refreshToken });
  t.check("refresh returns a new pair", rotated.status === 200 && rotated.json.data.refreshToken !== user.refreshToken);
  t.check("old refresh token is refused (401)", (await call("POST", "/auth/refresh-token", null, { refreshToken: user.refreshToken })).status === 401);
  t.check("reuse ended the newer session too (401)", (await call("POST", "/auth/refresh-token", null, { refreshToken: rotated.json.data.refreshToken })).status === 401);
  const session = (await call("POST", "/auth/login", null, { email: user.email, password: PASSWORD })).json.data;
  t.check("logout (200)", (await call("POST", "/auth/logout", null, { refreshToken: session.refreshToken })).status === 200);
  t.check("refresh after logout is refused (401)", (await call("POST", "/auth/refresh-token", null, { refreshToken: session.refreshToken })).status === 401);

  t.section("Protected endpoints");
  t.check("internal registration endpoint not exposed by the gateway", [404, 405].includes((await call("POST", "/users/internal/profiles", null, {})).status));
  t.check("user batch lookup needs a token (401)", (await call("POST", "/users/batch", null, [payload.sub])).status === 401);
  t.check("feed needs a token (401)", (await call("GET", "/posts/feed")).status === 401);
  t.check("notifications need a token (401)", (await call("GET", "/notifications")).status === 401);
  t.check("connections need a token (401)", (await call("GET", "/connections/me")).status === 401);

  return t.finish();
}
