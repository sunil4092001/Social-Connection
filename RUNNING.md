# Running BuildConnect

There are two ways to run the platform.

## 1. Everything in Docker (one command)

Requirements: Docker Desktop.

```powershell
./scripts/new-docker-env.ps1        # once: creates .env with random secrets (git-ignored)
docker compose up -d --build        # first run downloads images and builds; later runs are fast
```

| What | URL |
|---|---|
| Web app | http://localhost:4200 |
| API gateway + Swagger | http://localhost:5088/api · http://localhost:5088/swagger |
| Distributed traces (Jaeger) | http://localhost:16686 |
| Structured logs (Seq) | http://localhost:5380 |

The stack contains PostgreSQL, SQL Server, Neo4j, Kafka, Jaeger, Seq, the seven services and the Angular app.
Each service creates its own database schema at startup. Only the four ports above are published, so the stack
can run next to a locally installed PostgreSQL, SQL Server or Neo4j Desktop (it cannot run at the same time as the
local development services, which also use port 5088).

```powershell
docker compose ps                   # health of every container
docker compose logs -f postservice  # logs of one service
docker compose down                 # stop (data is kept in volumes)
docker compose down -v              # stop and delete all data
```

## 2. Local development (Visual Studio / dotnet run)

Requirements: .NET 9 SDK, Node 24, PostgreSQL, SQL Server, Neo4j Desktop (database `LinkedInDB`), Docker for Kafka.

```powershell
./scripts/setup-dev-secrets.ps1 -PostgresPassword <pwd> -Neo4jPassword <pwd>   # once per machine
docker compose -f docker-compose.dev.yml up -d                                  # Kafka + topics
dotnet ef database update --project <Service>/<Service>.csproj                  # when a service has new migrations
```

Start the services with the Visual Studio multi-start profile (or `dotnet run --launch-profile http` per service)
and the app with `npm start` in `linked/`. Secrets live in .NET user-secrets, never in `appsettings.json`.

## Health checks

Every service exposes:

- `GET /health` – liveness (process is running)
- `GET /health/ready` – readiness (its database or Neo4j answers)

## Tests

```powershell
dotnet test LinkedIn.Tests/LinkedIn.Tests.csproj
```

CI (`.github/workflows/ci.yml`) builds the solution, runs the tests, builds every service image and, when the
`linked/` sources are part of the checkout, builds the Angular app.

## Not part of the system

`ProductService` and `OrderService` are legacy projects kept in the solution. They are not started by
docker-compose or the development profile and nothing depends on them.
