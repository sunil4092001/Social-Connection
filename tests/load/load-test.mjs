// Load test through the API gateway: throughput and latency of BuildConnect's main read endpoints at increasing
// concurrency. No dependencies (Node 18+).
//
//   node tests/load/load-test.mjs                       # concurrency 1,10,50 for 20 s each
//   node tests/load/load-test.mjs --levels 1,25,100 --seconds 30
//
// It registers one e2e user (removed by tests/e2e/cleanup.ps1) and writes tests/load/results/load-<timestamp>.md.
import { mkdirSync, writeFileSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { call, customer, waitForStack, GATEWAY } from "../e2e/lib.mjs";

const arg = (name, fallback) => { const i = process.argv.indexOf(name); return i > 0 ? process.argv[i + 1] : fallback; };
const levels = arg("--levels", "1,10,50").split(",").map(Number);
const seconds = Number(arg("--seconds", "20"));

await waitForStack();
const user = await customer("load");
for (let i = 0; i < 20; i++) await call("POST", "/posts", user.token, { content: `Load test post ${i}` });
await call("POST", "/requirements", user.token, { title: "E2E load test requirement", description: "Load test", professionalType: "ROOFER", requiredSkillIds: [], location: "Pune" });

const scenarios = [
  { name: "GET /posts/feed (everyone, 20 posts + author names)", path: "/posts/feed?scope=all&pageSize=20", auth: true },
  { name: "GET /posts/feed (my network, calls ConnectionService)", path: "/posts/feed?scope=network&pageSize=20", auth: true },
  { name: "GET /notifications/unread-count", path: "/notifications/unread-count", auth: true },
  { name: "GET /requirements?status=OPEN (anonymous)", path: "/requirements?status=OPEN", auth: false },
  { name: "GET /users/me", path: "/users/me", auth: true },
];

const percentile = (sorted, p) => sorted.length ? sorted[Math.min(sorted.length - 1, Math.max(0, Math.ceil(p * sorted.length) - 1))] : 0;

async function measure(scenario, concurrency) {
  const latencies = [];
  let errors = 0;
  const deadline = Date.now() + seconds * 1000;
  const headers = scenario.auth ? { Authorization: `Bearer ${user.token}` } : {};
  const worker = async () => {
    while (Date.now() < deadline) {
      const start = performance.now();
      try {
        const response = await fetch(GATEWAY + scenario.path, { headers });
        await response.arrayBuffer();
        if (!response.ok) errors++;
      } catch { errors++; }
      latencies.push(performance.now() - start);
    }
  };
  const started = performance.now();
  await Promise.all(Array.from({ length: concurrency }, worker));
  const elapsed = (performance.now() - started) / 1000;
  latencies.sort((a, b) => a - b);
  return {
    scenario: scenario.name, concurrency, requests: latencies.length,
    rps: latencies.length / elapsed, p50: percentile(latencies, 0.5), p95: percentile(latencies, 0.95), p99: percentile(latencies, 0.99),
    errorRate: latencies.length ? errors / latencies.length : 0
  };
}

const results = [];
for (const scenario of scenarios) {
  for (const concurrency of levels) {
    await measure(scenario, Math.min(concurrency, 2)).catch(() => {}); // short warm-up
    const result = await measure(scenario, concurrency);
    results.push(result);
    console.log(`${scenario.name.padEnd(56)} c=${String(concurrency).padStart(3)}  ${result.rps.toFixed(1).padStart(7)} req/s  p50 ${result.p50.toFixed(1).padStart(7)} ms  p95 ${result.p95.toFixed(1).padStart(7)} ms  p99 ${result.p99.toFixed(1).padStart(7)} ms  errors ${(result.errorRate * 100).toFixed(2)}%`);
  }
}

const lines = [
  "# Load test results",
  "",
  `${new Date().toISOString()} · ${seconds} s per run · gateway ${GATEWAY} · concurrency = parallel clients sending back-to-back requests`,
  "",
  "| Endpoint | Concurrency | Requests | Throughput (req/s) | p50 (ms) | p95 (ms) | p99 (ms) | Errors |",
  "|---|---:|---:|---:|---:|---:|---:|---:|",
  ...results.map(r => `| ${r.scenario} | ${r.concurrency} | ${r.requests} | ${r.rps.toFixed(1)} | ${r.p50.toFixed(1)} | ${r.p95.toFixed(1)} | ${r.p99.toFixed(1)} | ${(r.errorRate * 100).toFixed(2)}% |`),
  ""
];
const directory = path.join(path.dirname(fileURLToPath(import.meta.url)), "results");
mkdirSync(directory, { recursive: true });
const file = path.join(directory, `load-${new Date().toISOString().replace(/[:.]/g, "-")}.md`);
writeFileSync(file, lines.join("\n"));
console.log(`\nResults: ${file}`);
