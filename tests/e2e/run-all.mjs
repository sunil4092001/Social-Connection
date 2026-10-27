// Runs every end-to-end suite against the running stack (local development or docker-compose).
//   node tests/e2e/run-all.mjs                 all suites
//   node tests/e2e/run-all.mjs posts           only suites whose file name contains "posts"
// Then remove the test users: ./tests/e2e/cleanup.ps1 (local) or `docker compose down -v` (docker).
import { readdirSync } from "node:fs";
import { fileURLToPath, pathToFileURL } from "node:url";
import path from "node:path";
import { waitForStack } from "./lib.mjs";

const directory = path.dirname(fileURLToPath(import.meta.url));
const filter = process.argv[2];
const files = readdirSync(directory).filter(f => /^\d\d-.*\.mjs$/.test(f) && (!filter || f.includes(filter))).sort();

await waitForStack();
let passed = 0, failed = 0;
const started = Date.now();
for (const file of files) {
  try {
    const { default: run } = await import(pathToFileURL(path.join(directory, file)));
    const result = await run();
    passed += result.passed;
    failed += result.failed;
  } catch (error) {
    failed++;
    console.log(`\n  FAIL ${file} crashed: ${error.message}`);
  }
}

console.log(`\n==== ${passed} passed, ${failed} failed (${files.length} suites, ${((Date.now() - started) / 1000).toFixed(0)} s)`);
process.exit(failed ? 1 : 0);
