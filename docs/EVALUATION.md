# BuildConnect – Evaluation

This document reports how the platform was verified and measured: functional correctness, reliability under
failure, performance under load, and a comparison of Neo4j and PostgreSQL for the network questions the platform
asks. Every number below was produced by scripts in this repository and can be reproduced (section 6).

**Test machine:** Intel Core i5-10210U (4 cores / 8 threads, 1.6 GHz base), 15.8 GB RAM, Windows 10, .NET 9,
PostgreSQL 18, Neo4j 5 (Neo4j Desktop, default memory settings), Kafka 3.9 in Docker, SQL Server 2022.
All services, databases and the load generator ran on this one machine.

## 1. Functional correctness

| Level | What | Result |
|---|---|---|
| Unit tests (`LinkedIn.Tests`, xUnit) | Domain rules (requirement state machine, connection rules), command/query handlers with in-memory fakes, validators, event → notification mapping, error mapping, refresh-token rotation, network matching bonus, trace propagation | **104 / 104 passed** |
| End-to-end tests (`tests/e2e`) | Real services, databases, Kafka and Neo4j, called through the API gateway exactly like the browser; each suite registers its own users | **80 / 80 passed** in 41 s |

End-to-end coverage by suite:

| Suite | Checks | Highlights |
|---|---:|---|
| Identity and security | 16 | refresh-token rotation and theft detection, logout, forged tokens rejected, internal endpoint not exposed |
| Requirements and notifications | 26 | full lifecycle OPEN → IN_PROGRESS → COMPLETED / CANCELLED, auto-rejection, every notification delivered via Kafka |
| Connections and network | 20 | only the addressee can accept, silent rejection, simultaneous opposite requests (exactly one wins), suggestions, network degrees, matching bonus (+10 / +5) |
| Posts | 18 | network feed follows connections, idempotent likes with one notification, comment permissions, soft delete |

## 2. Reliability under failure

These scenarios were exercised during development with the real services (see the phase reports).

| Failure | Expected behaviour | Observed |
|---|---|---|
| Kafka down while users act | Business actions succeed; events wait in the outbox and are published when Kafka returns | 10 events stored as pending while the broker was unreachable; relay retried with backoff and published them later |
| Poison message on a topic | Parked on the dead-letter topic; following events still processed | Invalid JSON landed on `notification-events-dlq`; the next real event was delivered ~2.7 s later |
| Same event delivered twice | One notification | Unique (EventId, recipient) index; like / unlike / like produced exactly one "liked" notification |
| ConnectionService down | Jobs can still be posted; matching runs without the network bonus; feed falls back to all posts | Job posted (201) with equal scores for all candidates; recommendations still served |
| UserService down during registration | No half-created account | 503 with a retry message and 0 orphan rows in IdentityDb |
| Two decisions at the same time | Only one succeeds | Optimistic concurrency (PostgreSQL `xmin`) and node locks in Neo4j; verified for opposite connection requests |

End-to-end latency of event-driven notifications (action → notification stored) was **about 2–3 s**, dominated by
the outbox relay's 2-second polling interval.

## 3. Performance under load

`tests/load/load-test.mjs`: parallel clients send back-to-back requests through the API gateway for 20 s per run.
Concurrency = number of parallel clients.

| Endpoint | Clients | Throughput (req/s) | p50 (ms) | p95 (ms) | p99 (ms) | Errors |
|---|---:|---:|---:|---:|---:|---:|
| Feed – everyone (20 posts, counts computed in SQL, author names from UserService) | 1 | 54 | 16.3 | 33.5 | 41.2 | 0 % |
| | 10 | 444 | 19.5 | 39.2 | 69.5 | 0 % |
| | 50 | 539 | 87.5 | 161.2 | 215.0 | 0 % |
| Feed – my network (adds a call to ConnectionService / Neo4j) | 1 | 38 | 26.5 | 45.1 | 54.9 | 0 % |
| | 10 | 251 | 37.8 | 73.7 | 91.7 | 0 % |
| | 50 | 316 | 157.9 | 243.9 | 299.0 | 0 % |
| Unread notification count | 1 | 65 | 15.6 | 25.4 | 30.7 | 0 % |
| | 10 | 1,353 | 6.8 | 14.5 | 23.6 | 0 % |
| | 50 | 1,671 | 29.0 | 50.8 | 68.5 | 0 % |
| Open requirements (anonymous) | 1 | 72 | 13.4 | 23.9 | 27.9 | 0 % |
| | 10 | 500 | 16.8 | 40.3 | 64.3 | 0 % |
| | 50 | 685 | 66.6 | 120.6 | 166.4 | 0 % |
| My profile | 1 | 63 | 15.6 | 26.3 | 31.6 | 0 % |
| | 10 | 1,020 | 9.0 | 16.8 | 28.1 | 0 % |
| | 50 | 1,001 | 45.5 | 86.5 | 131.8 | 0 % |

**Observations**

- **No errors in any run** (≈ 162,000 requests in total).
- Throughput grows 8–20× from 1 to 10 clients, then flattens between 10 and 50 clients while latency rises: at that
  point the single laptop's CPU, shared by seven services, the gateway, four databases and the load generator,
  is saturated. Horizontal scaling (more instances behind the gateway) is the next step and is possible because
  the services are stateless.
- **Each service-to-service hop has a cost.** The network feed does one more synchronous call than the public feed
  and reaches about 60 % of its throughput. This is the trade-off of strict data ownership; caching the caller's
  connection list for a short time would recover most of it.
- Simple single-service reads (unread count, profile) sustain 1,000–1,700 req/s with p95 below 90 ms.

## 4. Neo4j vs PostgreSQL for network questions

`benchmarks/GraphBenchmark` loads the **same** synthetic professional network into both databases and asks the four
questions ConnectionService answers. The network is generated by preferential attachment (Barabási–Albert,
10 new connections per user, average degree ≈ 20), which produces a few highly connected "hub" users as in real
social networks. Before timing, the tool checks that **both databases return identical answers** (they did at
every size). 10 warm-up runs, then 50 timed runs per query on randomly chosen users (the same users for both).

Modelling: Neo4j stores one undirected relationship per connection (as ConnectionService does); PostgreSQL uses an
adjacency table with both directions of every connection and a primary key on (user_a, user_b).

### Median latency (ms)

| Query | 1,000 users<br>Neo4j / PostgreSQL | 10,000 users<br>Neo4j / PostgreSQL | 50,000 users (≈ 500k connections)<br>Neo4j / PostgreSQL |
|---|---|---|---|
| People you may know (2 hops, top 10) | 12.3 / **8.7** | 7.9 / **2.9** | 6.9 / **3.8** |
| Mutual connections | 6.0 / **1.3** | 4.7 / **0.8** | 4.2 / **1.0** |
| Network degree of 50 users | 13.2 / **7.4** | 7.4 / **3.4** | 8.4 / **3.3** |
| Everyone within 3 hops | **7.7** / 40.1 | **9.7** / 35.9 | **21.7** / 69.2 |

### Tail latency – p95 (ms)

| Query | 1,000 | 10,000 | 50,000 |
|---|---|---|---|
| People you may know | 20.6 / **13.3** | 16.6 / **9.9** | 13.3 / **6.4** |
| Mutual connections | 7.4 / **1.7** | 6.3 / **0.9** | 5.4 / **1.2** |
| Network degree of 50 users | 21.8 / **15.1** | 12.8 / **10.8** | 13.0 / **8.0** |
| Everyone within 3 hops | **12.5** / 76.5 | **16.8** / 134.1 | **51.9** / 195.2 |

Loading (bulk import, 50,000 users): PostgreSQL 7.4 s (`COPY`), Neo4j 65.2 s (batched Cypher `UNWIND`).

### Interpretation

- **Up to two hops, PostgreSQL is faster in this setup** – 1.4–2.7× for suggestions and degree checks, 4–6× for mutual connections. These questions start from one user and touch only a
  few hundred rows; with a primary-key index, a relational join is very cheap, and the fixed per-query cost of the
  Neo4j driver and query engine (a few milliseconds) dominates. Both stay well within interactive limits (p95 below
  22 ms at every size).
- **For three hops, Neo4j is 3–5× faster and its tail latency grows far more slowly.** Following relationships is a
  pointer walk from the starting node in Neo4j, whereas PostgreSQL's recursive query repeatedly joins growing
  intermediate results; hub users make those results explode, which shows in PostgreSQL's p95 (76 → 134 → 195 ms).
- **Implication for BuildConnect.** The features in production today (suggestions, mutual connections, the
  matching trust bonus) need two hops, where a relational design would also perform well. Neo4j's advantage appears
  for deeper questions (3-hop reach, "who in my extended network has worked with this professional") and in
  modelling clarity: the Cypher queries are short path patterns that map one-to-one to the business questions, while
  the SQL equivalents need self-joins and `NOT EXISTS` sub-queries. Choosing Neo4j is therefore justified by the
  planned deeper network features and readability rather than by the current two-hop latency.
- **Bulk loading** favours PostgreSQL by about 9×; for Neo4j, `neo4j-admin database import` would be the right tool
  for initial loads. Day-to-day connection writes are single, small transactions in both systems.

### Threats to validity

- One machine runs both databases and the benchmark; absolute numbers will differ on servers, but both engines
  were measured under the same conditions with the same data, users and answers.
- Neo4j Desktop ran with default memory settings (heap and page cache were not tuned); PostgreSQL also used defaults.
- Data was warm (cached) after the warm-up runs; cold-start behaviour was not measured.
- The graph is synthetic. Preferential attachment reproduces hubs but not every property of a real professional
  network (for example, clustering by city or trade).
- Each query opens a session (Neo4j) or uses a pooled connection (Npgsql) as the services do; this per-query overhead
  is part of what an application experiences and is intentionally included.

## 5. Summary

| Requirement | Evidence |
|---|---|
| Correct business workflows | 104 unit tests and 80 end-to-end checks pass |
| Security | Ownership and role rules, refresh-token rotation with theft detection, secrets outside source control, all tested end to end |
| Reliability | Transactional outbox, idempotent consumers, dead-letter topic, graceful degradation; each failure scenario observed |
| Scalability | 300–1,700 req/s per endpoint on one laptop with 0 errors; stateless services ready for horizontal scaling; events decouple services |
| Polyglot persistence | Measured trade-off: PostgreSQL 1.4–6× faster for 1–2 hop questions, Neo4j 3–5× faster with much flatter tail latency for 3 hops |

## 6. Reproducing the results

```powershell
# Services running locally (see RUNNING.md), then:
node tests/e2e/run-all.mjs                                    # end-to-end suites
node tests/load/load-test.mjs --levels 1,10,50 --seconds 20   # load test -> tests/load/results/
$env:BENCH_NEO4J_PASSWORD = '<neo4j password>'
$env:BENCH_PG = 'Host=localhost;Username=postgres;Password=<pwd>'
dotnet run -c Release --project benchmarks/GraphBenchmark -- --sizes 1000,10000,50000 --runs 50 --warmup 10
./tests/e2e/cleanup.ps1 -PostgresPassword <pwd> -Neo4jPassword <pwd>   # remove the test users
dotnet test LinkedIn.Tests/LinkedIn.Tests.csproj               # unit tests
```

Raw results: `benchmarks/results/graph-benchmark-20261010-220253.{csv,md}` and
`tests/load/results/load-2026-10-10T16-26-43-407Z.md`.
