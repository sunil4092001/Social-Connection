> **Superseded.** This guide describes an earlier state of the code and is kept for history. The current architecture, workflows and operations are in [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md), [docs/EVALUATION.md](docs/EVALUATION.md) and [RUNNING.md](RUNNING.md).

# LinkedIn Microservices Project - End-to-End Guide

## 1. Project Overview

This project is a LinkedIn-style microservices backend built with .NET 9 and ASP.NET Core Web API.

The application is split into independent services. Each service owns its own business logic and database. Services communicate through APIs and messaging, not by directly reading another service's database.

Main services:

- UserService: manages LinkedIn user profiles.
- PostService: manages posts and feed data.
- ConnectionService: manages user connection requests and accepted connections.
- NotificationService: manages notifications.
- GatewayService: API gateway that routes requests to backend services.
- BuildingBlocks: shared infrastructure such as logging and exception middleware.
- LinkedIn.Tests: xUnit test project.

Existing older services also remain in the solution:

- IdentityService
- ProductService
- OrderService

Those older services were not removed, but the new LinkedIn-style services are the main domain services.

## 2. Technology Stack

The project uses:

- .NET 9: main runtime and framework.
- ASP.NET Core Web API: exposes HTTP endpoints.
- Clean Architecture: separates API, application, domain, and infrastructure responsibilities.
- CQRS with MediatR: separates commands and queries.
- Entity Framework Core: object-relational mapper for relational databases.
- PostgreSQL: used by UserService and PostService.
- Neo4j: used by ConnectionService.
- MySQL: used by NotificationService.
- Kafka: used as the messaging concept between services.
- Serilog: structured logging.
- FluentValidation: request and command validation.
- Swagger/OpenAPI: API documentation and testing UI.
- xUnit: automated tests.
- YARP: reverse proxy used by GatewayService.

## 3. Architecture

The project follows a microservices architecture.

Each microservice has:

- Its own API endpoints.
- Its own database.
- Its own repository layer.
- Its own business logic.
- Its own DTOs.
- Its own validation rules.

Important rule:

No service should access another service's database directly.

For example:

- PostService does not query UserService database.
- NotificationService does not query PostService database.
- ConnectionService does not query UserService database.

Instead, services should communicate through:

- HTTP APIs
- Kafka events

## 4. Clean Architecture Concepts

Clean Architecture separates code into layers.

### 4.1 API Layer

The API layer contains controllers.

Example:

- UsersController
- PostsController
- ConnectionsController
- NotificationsController

Responsibilities:

- Receive HTTP requests.
- Convert request data into commands or queries.
- Send commands/queries to MediatR.
- Return HTTP responses.

Controllers should not contain heavy business logic.

### 4.2 Application Layer

The application layer contains:

- Commands
- Queries
- Handlers
- DTOs
- Validators
- Interfaces

Responsibilities:

- Handle business use cases.
- Validate commands.
- Call repository interfaces.
- Return DTOs.

Example:

- CreateUserProfileCommand
- CreatePostCommand
- RequestConnectionCommand
- CreateNotificationCommand

### 4.3 Domain Layer

The domain layer contains core business entities.

Examples:

- UserProfile
- Post
- Notification

These classes represent important business objects.

### 4.4 Infrastructure Layer

The infrastructure layer contains database and external service implementations.

Examples:

- EfUserProfileRepository
- EfPostRepository
- Neo4jConnectionRepository
- EfNotificationRepository
- KafkaNotificationConsumer

Responsibilities:

- Save and read data.
- Connect to databases.
- Connect to external infrastructure such as Kafka.

## 5. CQRS Concept

CQRS means Command Query Responsibility Segregation.

It separates write operations and read operations.

### Commands

Commands change data.

Examples:

- CreateUserProfileCommand
- CreatePostCommand
- RequestConnectionCommand
- AcceptConnectionCommand
- CreateNotificationCommand
- MarkNotificationReadCommand

### Queries

Queries read data.

Examples:

- GetUserProfileByIdQuery
- SearchUserProfilesQuery
- GetFeedQuery
- GetConnectionsQuery
- GetNotificationsQuery

Why CQRS is used:

- Cleaner code organization.
- Easier testing.
- Better separation between reads and writes.
- Each use case has its own handler.

## 6. MediatR Concept

MediatR is used to decouple controllers from business logic.

Without MediatR:

Controller directly calls services or repositories.

With MediatR:

Controller sends a command/query.
MediatR finds the correct handler.
Handler performs the work.

Example flow:

1. Client calls POST /api/users.
2. UsersController creates CreateUserProfileCommand.
3. Controller sends command using IMediator.
4. CreateUserProfileCommandHandler handles the command.
5. Handler calls repository.
6. Repository saves data.
7. Handler returns DTO.
8. Controller returns HTTP response.

## 7. Repository Pattern

The repository pattern hides database logic behind an interface.

Example:

UserService uses:

- IUserProfileRepository
- EfUserProfileRepository

PostService uses:

- IPostRepository
- EfPostRepository

ConnectionService uses:

- IConnectionRepository
- Neo4jConnectionRepository

NotificationService uses:

- INotificationRepository
- EfNotificationRepository

Why repository pattern is used:

- Controllers and handlers do not know database details.
- Database code is easier to replace.
- Code is easier to test.
- Keeps business logic clean.

## 8. DTO Concept

DTO means Data Transfer Object.

DTOs are used to send data in and out of APIs.

Examples:

- CreateUserProfileRequest
- UserProfileDto
- CreatePostRequest
- PostDto
- ConnectionRequestDto
- ConnectionDto
- CreateNotificationRequest
- NotificationDto

Why DTOs are used:

- Avoid exposing database entities directly.
- Keep API contracts clean.
- Control what data is accepted from users.
- Control what data is returned to clients.

## 9. FluentValidation Concept

FluentValidation validates commands before business logic runs.

Examples:

CreateUserProfileValidator validates:

- DisplayName is required.
- DisplayName maximum length is 120.
- Headline is required.
- Headline maximum length is 160.

CreatePostValidator validates:

- AuthorUserId is required.
- Content is required.
- Content maximum length is 3000.

RequestConnectionValidator validates:

- RequesterUserId is required.
- AddresseeUserId is required.
- User cannot connect to themselves.

CreateNotificationValidator validates:

- RecipientUserId is required.
- Type is required.
- Message is required.

Why validation is used:

- Prevent invalid data from reaching the database.
- Keep business rules centralized.
- Return cleaner API errors.

## 10. Dependency Injection

Dependency Injection is used throughout the project.

Services are registered in Program.cs.

Example:

- IUserProfileRepository is registered with EfUserProfileRepository.
- IPostRepository is registered with EfPostRepository.
- IConnectionRepository is registered with Neo4jConnectionRepository.
- INotificationRepository is registered with EfNotificationRepository.

Why dependency injection is used:

- Reduces tight coupling.
- Makes testing easier.
- Allows replacing implementations.
- Keeps application code flexible.

## 11. Async/Await

Database and I/O operations use async/await.

Examples:

- AddAsync
- GetByIdAsync
- SearchAsync
- SaveChangesAsync
- ToListAsync

Why async/await is used:

- Improves scalability.
- Frees server threads while waiting for I/O.
- Handles many concurrent API requests better.

## 12. Databases Used and Why

### 12.1 PostgreSQL for UserService

UserService uses PostgreSQL.

Reason:

User profile data is structured and relational.

Example fields:

- Id
- DisplayName
- Headline
- About
- Location
- CreatedAtUtc

Why PostgreSQL fits:

- Strong relational database.
- Good for structured records.
- Reliable indexing and querying.
- Works well with Entity Framework Core.

### 12.2 PostgreSQL for PostService

PostService also uses PostgreSQL.

Reason:

Posts are structured records.

Example fields:

- Id
- AuthorUserId
- Content
- CreatedAtUtc

Why PostgreSQL fits:

- Good for ordered feed queries.
- Good for storing text content.
- Supports indexing by CreatedAtUtc.
- Reliable and scalable.

### 12.3 Neo4j for ConnectionService

ConnectionService uses Neo4j.

Reason:

Connections are graph data.

LinkedIn-style connections are naturally relationships:

- User A requested User B.
- User A is connected with User B.
- User B is connected with User A.

Why Neo4j fits:

- Designed for graph relationships.
- Easy to query connections.
- Good for future features such as mutual connections.
- Good for network traversal.

Possible future graph queries:

- Find mutual connections.
- Suggest people you may know.
- Find second-degree connections.

### 12.4 MySQL for NotificationService

NotificationService uses MySQL.

Reason:

Notifications are simple structured records.

Example fields:

- Id
- RecipientUserId
- Type
- Message
- IsRead
- CreatedAtUtc

Why MySQL fits:

- Good for simple transactional data.
- Easy to query notifications by recipient.
- Common choice for read/write operational data.
- Works with Entity Framework Core through Pomelo provider.

## 13. Kafka Messaging

Kafka is used as the messaging concept.

The project includes:

- LoggingIntegrationEventPublisher in PostService.
- KafkaNotificationConsumer in NotificationService.

Current behavior:

- PostService publishes/logs a posts.created integration event boundary.
- NotificationService has a Kafka consumer background service.

Why Kafka is used:

- Enables asynchronous communication.
- Reduces direct service coupling.
- Helps services react to events.
- Useful for notification workflows.

Example future flow:

1. User creates a post.
2. PostService publishes posts.created event.
3. NotificationService consumes the event.
4. NotificationService creates notifications for relevant users.

## 14. Swagger/OpenAPI

Each new service has Swagger enabled in development.

Swagger gives:

- API documentation.
- Endpoint list.
- Request/response schemas.
- Browser-based testing UI.

Swagger URLs when services run locally:

- UserService: https://localhost:7001/swagger
- PostService: https://localhost:7002/swagger
- ConnectionService: https://localhost:7003/swagger
- NotificationService: https://localhost:7004/swagger
- GatewayService: its configured launch URL plus /swagger

Actual ports can be changed in each service's Properties/launchSettings.json.

## 15. Serilog Logging

Serilog is used for structured logging.

The project uses shared logging setup from BuildingBlocks.

Why Serilog is used:

- Structured logs are easier to search.
- Supports log enrichment.
- Can send logs to Seq.
- Better for production diagnostics.

Seq URL configured:

http://localhost:5341

## 16. Global Middleware

BuildingBlocks contains shared middleware.

### CorrelationIdMiddleware

Used to track requests across services.

Why it matters:

- Helps trace one request across multiple services.
- Useful in microservices debugging.

### GlobalExceptionMiddleware

Handles unexpected exceptions in one place.

Why it matters:

- Centralizes error logging.
- Keeps controllers cleaner.

## 17. API Gateway

GatewayService uses YARP reverse proxy.

Its job:

- Accept external requests.
- Route them to internal services.

Configured gateway routes:

- /api/users/{**catch-all} routes to UserService.
- /api/posts/{**catch-all} routes to PostService.
- /api/connections/{**catch-all} routes to ConnectionService.
- /api/notifications/{**catch-all} routes to NotificationService.
- /api/auth/{**catch-all} routes to IdentityService.
- /api/products/{**catch-all} routes to ProductService.

Why gateway is used:

- Single entry point for clients.
- Hides internal service addresses.
- Central place for future auth, rate limiting, and routing.

## 18. Endpoints

### 18.1 UserService Endpoints

Base route:

```text
/api/users
```

Create user profile:

```http
POST /api/users
Content-Type: application/json

{
  "displayName": "Aarav Sharma",
  "headline": "Backend Developer",
  "about": "I build scalable .NET microservices.",
  "location": "Bengaluru, India"
}
```

Get user profile by id:

```http
GET /api/users/{id}
```

Search user profiles:

```http
GET /api/users?term=backend
```

### 18.2 PostService Endpoints

Base route:

```text
/api/posts
```

Create post:

```http
POST /api/posts
Content-Type: application/json

{
  "authorUserId": "11111111-1111-1111-1111-111111111111",
  "content": "Learning microservices with .NET 9!"
}
```

Get feed:

```http
GET /api/posts/feed?page=1&pageSize=20
```

### 18.3 ConnectionService Endpoints

Base route:

```text
/api/connections
```

Send connection request:

```http
POST /api/connections/requests
Content-Type: application/json

{
  "requesterUserId": "11111111-1111-1111-1111-111111111111",
  "addresseeUserId": "22222222-2222-2222-2222-222222222222"
}
```

Accept connection request:

```http
POST /api/connections/accept
Content-Type: application/json

{
  "requesterUserId": "11111111-1111-1111-1111-111111111111",
  "addresseeUserId": "22222222-2222-2222-2222-222222222222"
}
```

Get accepted connections:

```http
GET /api/connections/{userId}
```

### 18.4 NotificationService Endpoints

Base route:

```text
/api/notifications
```

Create notification:

```http
POST /api/notifications
Content-Type: application/json

{
  "recipientUserId": "11111111-1111-1111-1111-111111111111",
  "type": "PostCreated",
  "message": "Someone in your network created a post."
}
```

Get notifications for recipient:

```http
GET /api/notifications/{recipientUserId}
```

Mark notification as read:

```http
PATCH /api/notifications/{id}/read
```

## 19. How to Run the Project

### 19.1 Prerequisites

Install:

- .NET 9 SDK
- PostgreSQL
- Neo4j
- MySQL
- Kafka
- Optional: Seq for logs

Check .NET version:

```powershell
dotnet --version
```

### 19.2 Restore Packages

From the solution folder:

```powershell
cd D:\Microservice\Test\LinkedIn
dotnet restore LinkedIn.sln
```

### 19.3 Build Solution

```powershell
dotnet build LinkedIn.sln
```

### 19.4 Run Tests

```powershell
dotnet test LinkedIn.sln
```

### 19.5 Configure Databases

UserService connection string:

```json
"DefaultConnection": "Host=localhost;Port=5432;Database=linkedin_users;Username=postgres;Password=postgres"
```

PostService connection string:

```json
"DefaultConnection": "Host=localhost;Port=5432;Database=linkedin_posts;Username=postgres;Password=postgres"
```

NotificationService connection string:

```json
"DefaultConnection": "Server=localhost;Port=3306;Database=linkedin_notifications;User=root;Password=password;"
```

ConnectionService Neo4j settings:

```json
"Neo4j": {
  "Uri": "bolt://localhost:7687",
  "Username": "neo4j",
  "Password": "password"
}
```

Change passwords according to your local machine.

### 19.6 Create Databases

Create PostgreSQL databases:

```sql
CREATE DATABASE linkedin_users;
CREATE DATABASE linkedin_posts;
```

Create MySQL database:

```sql
CREATE DATABASE linkedin_notifications;
```

Neo4j does not need a relational database create command. It stores graph nodes and relationships.

### 19.7 Create EF Core Migrations

If migrations do not exist yet, create them:

```powershell
dotnet ef migrations add InitialCreate --project UserService\UserService.csproj
dotnet ef migrations add InitialCreate --project PostService\PostService.csproj
dotnet ef migrations add InitialCreate --project NotificationService\NotificationService.csproj
```

Apply migrations:

```powershell
dotnet ef database update --project UserService\UserService.csproj
dotnet ef database update --project PostService\PostService.csproj
dotnet ef database update --project NotificationService\NotificationService.csproj
```

### 19.8 Run Services

Open separate terminals.

Terminal 1:

```powershell
dotnet run --project UserService\UserService.csproj
```

Terminal 2:

```powershell
dotnet run --project PostService\PostService.csproj
```

Terminal 3:

```powershell
dotnet run --project ConnectionService\ConnectionService.csproj
```

Terminal 4:

```powershell
dotnet run --project NotificationService\NotificationService.csproj
```

Terminal 5:

```powershell
dotnet run --project GatewayService\GatewayService.csproj
```

## 20. Recommended Endpoint Testing Order

Use Swagger or Postman.

### Step 1: Create User 1

```http
POST /api/users
```

Body:

```json
{
  "displayName": "Aarav Sharma",
  "headline": "Backend Developer",
  "about": "I build scalable APIs.",
  "location": "Bengaluru"
}
```

Copy the returned user id.

### Step 2: Create User 2

```http
POST /api/users
```

Body:

```json
{
  "displayName": "Priya Mehta",
  "headline": "Cloud Engineer",
  "about": "I work with distributed systems.",
  "location": "Pune"
}
```

Copy the returned user id.

### Step 3: Create a Post

```http
POST /api/posts
```

Body:

```json
{
  "authorUserId": "PASTE_USER_1_ID_HERE",
  "content": "Hello LinkedIn-style microservices!"
}
```

### Step 4: Get Feed

```http
GET /api/posts/feed?page=1&pageSize=20
```

### Step 5: Send Connection Request

```http
POST /api/connections/requests
```

Body:

```json
{
  "requesterUserId": "PASTE_USER_1_ID_HERE",
  "addresseeUserId": "PASTE_USER_2_ID_HERE"
}
```

### Step 6: Accept Connection Request

```http
POST /api/connections/accept
```

Body:

```json
{
  "requesterUserId": "PASTE_USER_1_ID_HERE",
  "addresseeUserId": "PASTE_USER_2_ID_HERE"
}
```

### Step 7: Get Connections

```http
GET /api/connections/PASTE_USER_1_ID_HERE
```

### Step 8: Create Notification

```http
POST /api/notifications
```

Body:

```json
{
  "recipientUserId": "PASTE_USER_1_ID_HERE",
  "type": "ConnectionAccepted",
  "message": "Your connection request was accepted."
}
```

### Step 9: Get Notifications

```http
GET /api/notifications/PASTE_USER_1_ID_HERE
```

### Step 10: Mark Notification Read

```http
PATCH /api/notifications/PASTE_NOTIFICATION_ID_HERE/read
```

## 21. Important Project Rules

The project follows these rules:

- Use async/await.
- Use dependency injection.
- Use DTOs.
- Use FluentValidation.
- Use repository pattern.
- Use MediatR for CQRS.
- Generate Swagger documentation.
- Do not access another service's database.
- Each microservice owns its own database.
- Use Kafka for cross-service events.
- Use Serilog for logging.

## 22. Build and Test Result

The solution was verified with:

```powershell
dotnet restore LinkedIn.sln
dotnet build LinkedIn.sln --no-restore
dotnet test LinkedIn.sln --no-restore --no-build
```

Result:

- Build succeeded.
- Tests passed.
- 4 validation tests passed.

## 23. Future Improvements

Recommended next improvements:

- Add authentication and authorization to new services.
- Add real Kafka event publishing in PostService.
- Convert NotificationService consumer from logging to real notification creation.
- Add Docker Compose for PostgreSQL, Neo4j, MySQL, Kafka, Seq, and services.
- Add health checks.
- Add API versioning.
- Add centralized gateway authentication.
- Add integration tests with test containers.
- Add migrations to source control.
- Add distributed tracing.

