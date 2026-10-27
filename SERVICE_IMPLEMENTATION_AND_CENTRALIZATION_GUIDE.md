> **Superseded.** This guide describes an earlier state of the code and is kept for history. The current architecture, workflows and operations are in [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md), [docs/EVALUATION.md](docs/EVALUATION.md) and [RUNNING.md](RUNNING.md).

# LinkedIn Microservices - Service Implementation and Centralization Guide

## 1. Purpose of This Guide

This guide explains how each service in this project is implemented and how the microservices should work together.

It also explains two important architecture questions:

- Why IdentityService and UserService have separate databases.
- How to centralize logging, login, and registration in a microservices system.

## 2. High-Level Architecture

This project follows a microservices architecture.

Each service owns one business capability:

- IdentityService owns authentication and credentials.
- UserService owns public user profile data.
- PostService owns posts and feed data.
- ConnectionService owns user relationship graph data.
- NotificationService owns notification records.
- GatewayService exposes a single entry point for clients.
- BuildingBlocks contains shared infrastructure code.

Important rule:

No service should directly access another service's database.

For example:

- UserService should not read IdentityService database.
- PostService should not read UserService database.
- NotificationService should not read PostService database.

Services should communicate using:

- HTTP APIs through GatewayService.
- Kafka events for asynchronous workflows.

## 3. Solution Structure

Main folders:

```text
BuildingBlocks
GatewayService
IdentityService
UserService
PostService
ConnectionService
NotificationService
LinkedIn.Tests
```

Older existing folders:

```text
ProductService
OrderService
```

The LinkedIn-style business services are:

```text
IdentityService
UserService
PostService
ConnectionService
NotificationService
GatewayService
```

## 4. IdentityService Implementation

IdentityService is responsible for authentication.

It handles:

- User registration.
- User login.
- Password hashing.
- JWT access token generation.
- Refresh token generation.
- Refresh token validation.
- Logout by revoking refresh token.

### 4.1 Main Files

```text
IdentityService/Controllers/AuthController.cs
IdentityService/Services/AuthService.cs
IdentityService/Services/JwtService.cs
IdentityService/Repository/UserRepository.cs
IdentityService/Data/IdentityDbContext.cs
IdentityService/Entities/User.cs
IdentityService/Entities/RefreshToken.cs
IdentityService/DTOs/RegisterRequest.cs
IdentityService/DTOs/LoginRequest.cs
IdentityService/DTOs/LoginResponse.cs
```

### 4.2 AuthController

AuthController exposes authentication endpoints.

Base route:

```text
/api/auth
```

Important endpoints:

```http
POST /api/auth/register
POST /api/auth/login
POST /api/auth/refresh-token
POST /api/auth/logout
GET  /api/auth/profile
```

The controller does not directly talk to the database.
It calls AuthService.

### 4.3 AuthService

AuthService contains authentication business logic.

Register flow:

1. Check if email already exists.
2. Hash password using BCrypt.
3. Create User entity.
4. Save user through repository.

Login flow:

1. Find user by email.
2. Verify password using BCrypt.
3. Generate JWT access token.
4. Generate refresh token.
5. Save refresh token.
6. Return LoginResponse.

### 4.4 JwtService

JwtService generates:

- Access tokens.
- Refresh tokens.

Access tokens are used by clients to call protected APIs.

Refresh tokens are used to get a new access token without logging in again.

### 4.5 Identity Database

IdentityService has its own PostgreSQL database.

It stores private authentication data:

- Email
- PasswordHash
- RefreshTokens
- User identity fields

This database should be treated as sensitive.

## 5. UserService Implementation

UserService is responsible for LinkedIn public profile data.

It handles:

- Create user profile.
- Get user profile by id.
- Search user profiles.

### 5.1 Main Files

```text
UserService/Controllers/UsersController.cs
UserService/Domain/UserProfile.cs
UserService/Application/Profiles/CreateUserProfileCommand.cs
UserService/Application/Profiles/CreateUserProfileCommandHandler.cs
UserService/Application/Profiles/GetUserProfileByIdQuery.cs
UserService/Application/Profiles/SearchUserProfilesQuery.cs
UserService/Application/Profiles/CreateUserProfileValidator.cs
UserService/Application/Dtos/CreateUserProfileRequest.cs
UserService/Application/Dtos/UserProfileDto.cs
UserService/Repositories/IUserProfileRepository.cs
UserService/Infrastructure/Persistence/UserDbContext.cs
UserService/Infrastructure/Repositories/EfUserProfileRepository.cs
```

### 5.2 UsersController

Base route:

```text
/api/users
```

Endpoints:

```http
POST /api/users
GET  /api/users/{id}
GET  /api/users?term=value
```

The controller sends commands and queries through MediatR.

### 5.3 Application Layer

Commands:

```text
CreateUserProfileCommand
```

Queries:

```text
GetUserProfileByIdQuery
SearchUserProfilesQuery
```

Validator:

```text
CreateUserProfileValidator
```

The validator checks:

- DisplayName is required.
- Headline is required.
- Length limits are respected.

### 5.4 User Database

UserService uses PostgreSQL.

It stores public profile data:

- DisplayName
- Headline
- About
- Location
- CreatedAtUtc

This is separate from IdentityService because authentication and profile management are different business capabilities.

## 6. Why IdentityService and UserService Have Separate Databases

This is correct for microservices.

IdentityService database stores login/security data.

UserService database stores public profile data.

They should be separate because:

- Authentication data is sensitive.
- Profile data changes for different business reasons.
- Each service can evolve independently.
- Each service owns its own schema.
- Other services should not touch identity tables.

Example:

IdentityService knows:

```text
Email
PasswordHash
RefreshToken
```

UserService knows:

```text
DisplayName
Headline
About
Location
```

They may share the same logical user id, but they should not share the same database tables.

## 7. How to Connect Identity User and User Profile

Best approach:

Use the IdentityService user id as the profile owner id in UserService.

Recommended UserProfile design:

```text
Id              Profile id
IdentityUserId  User id from IdentityService
DisplayName
Headline
About
Location
CreatedAtUtc
```

Current UserService creates its own profile id.

Recommended improvement:

After registration:

1. IdentityService creates identity user.
2. IdentityService publishes UserRegistered event to Kafka.
3. UserService consumes UserRegistered event.
4. UserService creates an empty/default profile linked to IdentityUserId.

Alternative simple approach:

1. Client registers through IdentityService.
2. Client logs in and receives JWT.
3. Client calls UserService to create profile.
4. UserService reads user id from JWT claim.
5. UserService stores that id as IdentityUserId.

Do not make UserService query IdentityService database directly.

## 8. PostService Implementation

PostService is responsible for posts and feed data.

It handles:

- Create post.
- Get feed.
- Publish post-created integration event boundary.

### 8.1 Main Files

```text
PostService/Controllers/PostsController.cs
PostService/Domain/Post.cs
PostService/Application/PostContracts.cs
PostService/Application/ValidationBehavior.cs
PostService/Infrastructure/PostDbContext.cs
PostService/Infrastructure/EfPostRepository.cs
PostService/Infrastructure/LoggingIntegrationEventPublisher.cs
```

### 8.2 PostsController

Base route:

```text
/api/posts
```

Endpoints:

```http
POST /api/posts
GET  /api/posts/feed?page=1&pageSize=20
```

### 8.3 Application Layer

Commands:

```text
CreatePostCommand
```

Queries:

```text
GetFeedQuery
```

Validator:

```text
CreatePostValidator
```

### 8.4 Post Database

PostService uses PostgreSQL.

It stores:

- Post id
- AuthorUserId
- Content
- CreatedAtUtc

PostgreSQL is good here because posts are structured relational records and feed queries need ordering and indexing.

## 9. ConnectionService Implementation

ConnectionService is responsible for user relationships.

It handles:

- Send connection request.
- Accept connection request.
- Get accepted connections.

### 9.1 Main Files

```text
ConnectionService/Controllers/ConnectionsController.cs
ConnectionService/Application/ConnectionContracts.cs
ConnectionService/Application/ValidationBehavior.cs
ConnectionService/Infrastructure/Neo4jConnectionRepository.cs
ConnectionService/Infrastructure/Neo4jOptions.cs
ConnectionService/appsettings.json
```

### 9.2 ConnectionsController

Base route:

```text
/api/connections
```

Endpoints:

```http
POST /api/connections/requests
POST /api/connections/accept
GET  /api/connections/{userId}
```

### 9.3 Neo4j Repository

Neo4jConnectionRepository runs Cypher queries.

Request connection creates:

```text
(requester:User)-[:REQUESTED]->(addressee:User)
```

Accept connection creates:

```text
(requester)-[:CONNECTED_WITH]->(addressee)
(addressee)-[:CONNECTED_WITH]->(requester)
```

### 9.4 Neo4j Database

ConnectionService uses Neo4j.

Current config:

```json
"Neo4j": {
  "Uri": "neo4j://127.0.0.1:7687",
  "Database": "LinkedInDB",
  "Username": "neo4j",
  "Password": "password"
}
```

Neo4j is used because social connections are graph relationships.

Good future use cases:

- Mutual connections.
- People you may know.
- Second-degree connections.
- Relationship path queries.

No EF migration is needed for Neo4j.

## 10. NotificationService Implementation

NotificationService is responsible for notification records.

It handles:

- Create notification.
- Get notifications for a user.
- Mark notification as read.
- Kafka consumer boundary.

### 10.1 Main Files

```text
NotificationService/Controllers/NotificationsController.cs
NotificationService/Domain/Notification.cs
NotificationService/Application/NotificationContracts.cs
NotificationService/Application/ValidationBehavior.cs
NotificationService/Infrastructure/NotificationDbContext.cs
NotificationService/Infrastructure/EfNotificationRepository.cs
NotificationService/Infrastructure/KafkaNotificationConsumer.cs
```

### 10.2 NotificationsController

Base route:

```text
/api/notifications
```

Endpoints:

```http
POST  /api/notifications
GET   /api/notifications/{recipientUserId}
PATCH /api/notifications/{id}/read
```

### 10.3 Notification Database

NotificationService is now configured for SQL Server.

Current connection string:

```json
"DefaultConnection": "Server=localhost;Database=linkedin_notifications;Trusted_Connection=True;TrustServerCertificate=True;"
```

It stores:

- Notification id
- RecipientUserId
- Type
- Message
- IsRead
- CreatedAtUtc

SQL Server is fine for notifications because this data is relational and queryable by recipient user id.

## 11. GatewayService Implementation

GatewayService uses YARP reverse proxy.

Its job:

- Provide one entry point for clients.
- Route requests to internal services.
- Hide internal service ports.

### 11.1 Main Files

```text
GatewayService/Program.cs
GatewayService/appsettings.json
```

### 11.2 Gateway Routes

Gateway routes:

```text
/api/auth/**           -> IdentityService
/api/users/**          -> UserService
/api/posts/**          -> PostService
/api/connections/**    -> ConnectionService
/api/notifications/**  -> NotificationService
```

This means clients should ideally call GatewayService, not individual services.

Example:

```http
POST https://localhost:7211/api/auth/login
POST https://localhost:7211/api/users
GET  https://localhost:7211/api/posts/feed
```

## 12. BuildingBlocks Implementation

BuildingBlocks contains shared infrastructure.

Main files:

```text
BuildingBlocks/Logging/SerilogExtensions.cs
BuildingBlocks/Logging/CorrelationIdMiddleware.cs
BuildingBlocks/ExceptionHandling/GlobalExceptionMiddleware.cs
```

### 12.1 SerilogExtensions

Each service calls:

```csharp
builder.AddSerilogLogging("ServiceName");
```

This configures:

- Console logging.
- Seq logging.
- ServiceName enrichment.
- MachineName enrichment.
- EnvironmentName enrichment.

### 12.2 CorrelationIdMiddleware

This middleware reads or creates:

```text
X-Correlation-Id
```

The correlation id is written into logs.

This helps track a single request across services.

### 12.3 GlobalExceptionMiddleware

This catches unhandled exceptions, logs them, and keeps exception logging centralized.

## 13. Centralized Logging

Centralized logging means all services send logs to one place.

In this project, the recommended central log tool is Seq.

Configured URL:

```text
http://localhost:5341
```

Each service has this config:

```json
"Seq": {
  "ServerUrl": "http://localhost:5341"
}
```

And each service calls:

```csharp
builder.AddSerilogLogging("ServiceName");
```

### 13.1 How to Run Seq

Using Docker:

```powershell
docker run --name seq -d -p 5341:80 -e ACCEPT_EULA=Y datalust/seq
```

Open:

```text
http://localhost:5341
```

### 13.2 Why Centralized Logging Matters

Microservices run as separate processes.

Without centralized logging, you must check every terminal separately.

With Seq:

- IdentityService logs appear in one place.
- UserService logs appear in one place.
- PostService logs appear in one place.
- ConnectionService logs appear in one place.
- NotificationService logs appear in one place.
- GatewayService logs appear in one place.

You can filter by:

```text
ServiceName
CorrelationId
RequestPath
Exception
```

### 13.3 Recommended Logging Flow

Client sends request:

```text
X-Correlation-Id: abc-123
```

Gateway receives request and logs it.

Downstream service receives same correlation id and logs it.

In Seq, search:

```text
CorrelationId = 'abc-123'
```

This shows the full request journey.

## 14. Centralized Login and Register

In microservices, login and register should be centralized in IdentityService.

Do not implement login/register separately in every service.

Correct design:

```text
Client -> GatewayService -> IdentityService
```

Endpoints:

```http
POST /api/auth/register
POST /api/auth/login
POST /api/auth/refresh-token
POST /api/auth/logout
```

### 14.1 Gateway as Single Entry Point

The client should call:

```http
POST https://localhost:7211/api/auth/register
POST https://localhost:7211/api/auth/login
```

Gateway forwards to:

```text
IdentityService
```

### 14.2 JWT Authentication for Other Services

After login, IdentityService returns an access token.

Client sends that token to other APIs:

```http
Authorization: Bearer YOUR_ACCESS_TOKEN
```

Recommended next improvement:

Add JWT authentication setup to:

- UserService
- PostService
- ConnectionService
- NotificationService

Then protect endpoints with:

```csharp
[Authorize]
```

### 14.3 Recommended User Registration Flow

Best production-style flow:

1. Client calls GatewayService `/api/auth/register`.
2. Gateway forwards to IdentityService.
3. IdentityService creates login account.
4. IdentityService publishes `UserRegistered` Kafka event.
5. UserService consumes the event.
6. UserService creates default profile record.
7. Client logs in.
8. Client updates profile through UserService.

This keeps IdentityService and UserService separate but synchronized.

### 14.4 Simpler Current Flow

For now, you can test like this:

1. Register in IdentityService.
2. Login in IdentityService.
3. Create profile in UserService.
4. Use returned profile id/user id in PostService, ConnectionService, and NotificationService.

## 15. What Should Be Centralized and What Should Not

Centralize:

- Login.
- Register.
- Token generation.
- Token refresh.
- Token validation rules.
- Logging collection.
- Gateway routing.
- Correlation id handling.

Do not centralize:

- Every service's database.
- Every service's business logic.
- Every service's entities.
- Direct database access.

Microservices should share contracts and events, not database tables.

## 16. Recommended Improvements for This Project

### 16.1 Add Shared JWT Setup

Create a shared extension in BuildingBlocks:

```text
BuildingBlocks/Authentication/JwtAuthenticationExtensions.cs
```

Then each service can call:

```csharp
builder.Services.AddJwtAuthentication(builder.Configuration);
```

This prevents duplicated JWT setup.

### 16.2 Add Gateway Authentication

Gateway can validate tokens before forwarding requests.

Recommended:

- `/api/auth/register` public
- `/api/auth/login` public
- `/api/users/**` protected
- `/api/posts/**` protected
- `/api/connections/**` protected
- `/api/notifications/**` protected

### 16.3 Add Kafka UserRegistered Event

IdentityService should publish:

```json
{
  "eventName": "UserRegistered",
  "userId": 1,
  "email": "user@example.com",
  "occurredAtUtc": "2026-06-22T10:00:00Z"
}
```

UserService should consume it and create profile shell.

### 16.4 Add Real Kafka Publisher

Current PostService has a logging integration event publisher boundary.

Recommended:

- Replace LoggingIntegrationEventPublisher with KafkaIntegrationEventPublisher.
- Publish events to Kafka topics.
- NotificationService consumes events and creates notifications.

### 16.5 Add Docker Compose

Recommended containers:

- PostgreSQL
- SQL Server
- Neo4j
- Kafka
- Seq
- All services

This makes the whole system easier to run.

## 17. Current Run URLs

Direct service URLs:

```text
IdentityService:      https://localhost:5001
UserService:          https://localhost:7245
PostService:          https://localhost:7179
ConnectionService:    https://localhost:7069
NotificationService:  https://localhost:7234
GatewayService:       https://localhost:7211
```

Swagger URLs:

```text
https://localhost:5001/swagger
https://localhost:7245/swagger
https://localhost:7179/swagger
https://localhost:7069/swagger
https://localhost:7234/swagger
https://localhost:7211/swagger
```

## 18. Summary

IdentityService is the central authentication service.

UserService is the profile service and should remain separate from IdentityService.

Centralized logging is already mostly implemented through BuildingBlocks, Serilog, correlation ids, and Seq.

GatewayService should be the central external entry point for clients.

The best next architecture step is to add JWT authentication consistently to all new services and use Kafka events to synchronize IdentityService registration with UserService profile creation.

