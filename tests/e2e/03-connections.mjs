// Connection lifecycle in Neo4j, people you may know, network degrees and the matching trust bonus.
import { call, connect, customer, roofer, suite, waitForNotification } from "./lib.mjs";

export default async function run() {
  const t = suite("Connections and network");
  const [a, b, c, bridge] = await Promise.all([customer("con-a"), customer("con-b"), customer("con-c"), customer("con-bridge")]);
  const status = async (me, other) => (await call("GET", `/connections/status/${other.id}`, me.token)).json?.data;

  t.section("Request and accept");
  t.check("self connection is invalid (400)", (await call("POST", `/connections/request?addresseeUserId=${a.id}`, a.token)).status === 400);
  t.check("unknown user is not found (404)", (await call("POST", `/connections/request?addresseeUserId=${crypto.randomUUID()}`, a.token)).status === 404);
  t.check("A requests B (200)", (await call("POST", `/connections/request?addresseeUserId=${b.id}`, a.token)).status === 200);
  t.check("duplicate request (409)", (await call("POST", `/connections/request?addresseeUserId=${b.id}`, a.token)).status === 409);
  t.check("reverse request is refused (409)", (await call("POST", `/connections/request?addresseeUserId=${a.id}`, b.token)).status === 409);
  t.check("statuses PENDING_SENT / PENDING_RECEIVED", (await status(a, b))?.status === "PENDING_SENT" && (await status(b, a))?.status === "PENDING_RECEIVED");
  t.check("B is notified", (await waitForNotification(b, n => n.type === "ConnectionRequested" && n.referenceId === a.id)).length === 1);
  t.check("someone else cannot accept it (404)", (await call("POST", "/connections/accept", c.token, { requesterUserId: a.id })).status === 404);
  t.check("B accepts (200, CONNECTED)", (await call("POST", "/connections/accept", b.token, { requesterUserId: a.id })).json?.data?.status === "CONNECTED");
  t.check("A is notified", (await waitForNotification(a, n => n.type === "ConnectionAccepted" && n.referenceId === b.id)).length === 1);

  t.section("Reject, withdraw, remove");
  await call("POST", `/connections/request?addresseeUserId=${c.id}`, a.token);
  t.check("C rejects (204)", (await call("POST", "/connections/reject", c.token, { requesterUserId: a.id })).status === 204);
  t.check("rejection is silent: A sees NONE", (await status(a, c))?.status === "NONE");
  t.check("A may ask again and withdraw (200, 204)", (await call("POST", `/connections/request?addresseeUserId=${c.id}`, a.token)).status === 200
    && (await call("DELETE", `/connections/sent-requests/${c.id}`, a.token)).status === 204);
  const [x, y] = await Promise.all([
    call("POST", `/connections/request?addresseeUserId=${bridge.id}`, c.token),
    call("POST", `/connections/request?addresseeUserId=${c.id}`, bridge.token)]);
  t.check("simultaneous opposite requests: exactly one wins", [x.status, y.status].sort().join(",") === "200,409", `${x.status},${y.status}`);

  t.section("Network intelligence");
  // Network: A - B (connected above), A - bridge, bridge - roofer2. Roofer3 is outside A's network.
  const [r1, r2, r3] = await Promise.all([roofer("net-r1"), roofer("net-r2"), roofer("net-r3")]);
  await connect(a, r1);
  await connect(a, bridge);
  await connect(bridge, r2);
  const suggestions = (await call("GET", "/connections/suggestions", a.token)).json?.data ?? [];
  t.check("friend-of-friend is suggested with 1 mutual connection", suggestions.some(s => s.userId === r2.id && s.mutualConnectionCount === 1), suggestions);
  t.check("existing connections are not suggested", !suggestions.some(s => [b.id, r1.id, bridge.id].includes(s.userId)));
  const degrees = Object.fromEntries(((await call("POST", "/connections/degrees", a.token, { userIds: [r1.id, r2.id, r3.id] })).json?.data ?? []).map(d => [d.userId, d.degree]));
  t.check("degrees: 1st, 2nd, outside", degrees[r1.id] === 1 && degrees[r2.id] === 2 && !(r3.id in degrees), degrees);
  const job = (await call("POST", "/requirements", a.token, { title: "E2E roof inspection", description: "Inspect roof", professionalType: "ROOFER", requiredSkillIds: [], location: "Pune" })).json.data.id;
  const scoreFor = async (r) => ((await call("GET", "/requirements/recommended", r.token)).json?.data ?? []).find(item => item.id === job);
  const [s1, s2, s3] = [await scoreFor(r1), await scoreFor(r2), await scoreFor(r3)];
  t.check("trust bonus: 1st degree +10, 2nd degree +5 over a stranger", s1?.matchScore - s3?.matchScore === 10 && s2?.matchScore - s3?.matchScore === 5, { s1, s2, s3 });
  t.check("recommendations report the network degree", s1?.networkDegree === 1 && s2?.networkDegree === 2 && s3?.networkDegree == null);
  await call("DELETE", `/requirements/${job}`, a.token);

  t.check("remove connection (204, then NONE)", (await call("DELETE", `/connections/${b.id}`, a.token)).status === 204 && (await status(a, b))?.status === "NONE");
  return t.finish();
}
