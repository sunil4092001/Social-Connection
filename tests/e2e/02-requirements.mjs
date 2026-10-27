// Requirement lifecycle across RequirementService -> outbox -> Kafka -> NotificationService.
import { call, customer, roofer, suite, waitForNotification } from "./lib.mjs";

export default async function run() {
  const t = suite("Requirements and notifications");
  const owner = await customer("req-owner");
  const [r1, r2, r3] = await Promise.all([roofer("req-r1", "Pune"), roofer("req-r2", "Pune"), roofer("req-r3", "Pune")]);
  const job = { title: "E2E roof waterproofing", description: "Waterproof a 1,200 sq ft terrace", professionalType: "ROOFER", requiredSkillIds: [], location: "Pune" };

  t.section("Create and discover");
  t.check("a professional cannot post a requirement (403)", (await call("POST", "/requirements", r1.token, job)).status === 403);
  const created = await call("POST", "/requirements", owner.token, job);
  const id = created.json?.data?.id;
  t.check("customer posts a requirement (201, OPEN)", created.status === 201 && created.json.data.status === "OPEN");
  const matched = await waitForNotification(r1, n => n.referenceId === id && n.type === "NewRequirementMatch");
  t.check("matching roofer is notified (requirement-created via Kafka)", matched.length === 1);
  t.check("it is in the roofer's recommendations", ((await call("GET", "/requirements/recommended", r1.token)).json?.data ?? []).some(r => r.id === id));

  t.section("Interest");
  const i1 = await call("POST", `/requirements/${id}/interest`, r1.token, { message: "Available Monday" });
  const i2 = await call("POST", `/requirements/${id}/interest`, r2.token, {});
  await call("POST", `/requirements/${id}/interest`, r3.token, {});
  t.check("three roofers express interest (200)", i1.status === 200 && i2.status === 200);
  t.check("duplicate interest is refused (409)", (await call("POST", `/requirements/${id}/interest`, r1.token, {})).status === 409);
  t.check("a customer cannot express interest (403)", (await call("POST", `/requirements/${id}/interest`, owner.token, {})).status === 403);
  const anonymous = (await call("GET", `/requirements/${id}`)).json?.data;
  t.check("anonymous viewers see the count but no responses", anonymous?.responseCount === 3 && anonymous?.responses?.length === 0);
  t.check("another professional cannot list responses (403)", (await call("GET", `/requirements/${id}/responses`, r1.token)).status === 403);
  const responses = (await call("GET", `/requirements/${id}/responses`, owner.token)).json?.data ?? [];
  t.check("owner sees all responses with names", responses.length === 3 && responses.every(r => r.professionalName));
  t.check("owner is notified of interest", (await waitForNotification(owner, n => n.referenceId === id && n.type === "ProfessionalInterested")).length >= 1);

  t.section("Decision");
  const decide = (who, response, status) => call("PUT", `/requirements/${id}/responses/${response}`, who.token, { status });
  t.check("INTERESTED is not a decision (400)", (await decide(owner, i1.json.data.id, "INTERESTED")).status === 400);
  t.check("only the owner decides (403)", (await decide(r2, i1.json.data.id, "ACCEPTED")).status === 403);
  t.check("owner hires roofer 1 (200)", (await decide(owner, i1.json.data.id, "ACCEPTED")).status === 200);
  const after = (await call("GET", `/requirements/${id}`, owner.token)).json?.data;
  t.check("requirement is IN_PROGRESS and others were auto-rejected", after?.status === "IN_PROGRESS" && after.responses.filter(r => r.status === "REJECTED").length === 2);
  t.check("hired roofer is notified", (await waitForNotification(r1, n => n.referenceId === id && n.type === "InterestAccepted")).length === 1);
  t.check("auto-rejected roofer is notified", (await waitForNotification(r2, n => n.referenceId === id && n.type === "InterestRejected")).length === 1);
  t.check("a decided response cannot change (409)", (await decide(owner, i2.json.data.id, "ACCEPTED")).status === 409);

  t.section("Finish and cancel");
  t.check("cannot edit while in progress (409)", (await call("PUT", `/requirements/${id}`, owner.token, { ...job, title: "changed" })).status === 409);
  t.check("owner completes (200, COMPLETED)", (await call("POST", `/requirements/${id}/complete`, owner.token)).json?.data?.status === "COMPLETED");
  t.check("completed requirement cannot be cancelled (409)", (await call("DELETE", `/requirements/${id}`, owner.token)).status === 409);
  const second = (await call("POST", "/requirements", owner.token, { ...job, title: "E2E roof repair" })).json.data.id;
  await call("POST", `/requirements/${second}/interest`, r3.token, {});
  t.check("cancel an open requirement (204)", (await call("DELETE", `/requirements/${second}`, owner.token)).status === 204);
  t.check("interested roofer is notified of the cancellation", (await waitForNotification(r3, n => n.referenceId === second && n.type === "RequirementCancelled")).length === 1);

  t.section("Notification API");
  const unread = (await call("GET", "/notifications/unread-count", r1.token)).json?.data;
  t.check("unread count is positive", unread > 0);
  const anyNotification = (await call("GET", "/notifications?pageSize=1", r1.token)).json.data.items[0];
  t.check("another user cannot mark it read (404)", (await call("PATCH", `/notifications/${anyNotification.id}/read`, r2.token)).status === 404);
  await call("PATCH", "/notifications/read-all", r1.token);
  t.check("mark all read -> 0 unread", (await call("GET", "/notifications/unread-count", r1.token)).json?.data === 0);

  return t.finish();
}
