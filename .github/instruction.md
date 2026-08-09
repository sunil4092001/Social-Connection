# LinkedIn Clone - Copilot Instructions

## Technology Stack

* .NET 9
* ASP.NET Core Web API
* Clean Architecture
* CQRS with MediatR
* Entity Framework Core
* JWT Authentication
* Apache Kafka
* YARP API Gateway
* Serilog
* OpenTelemetry
* FluentValidation
* xUnit
* Docker

## Database Strategy

* UserService → PostgreSQL
* PostService → PostgreSQL
* ConnectionService → Neo4j
* NotificationService → MySQL

## Architecture Rules

* Each service owns its own database
* No service may directly access another service database
* Communication between services uses REST APIs and Kafka events
* Follow Domain-Driven Design principles
* Follow SOLID principles

## Clean Architecture Rules

Each service must contain:

* Api
* Application
* Domain
* Infrastructure
* Tests

Dependencies:

* Api → Application
* Infrastructure → Application + Domain
* Application → Domain

## Coding Standards

* Use async/await
* Use dependency injection
* Use constructor injection
* Use file-scoped namespaces
* Enable nullable reference types
* Use cancellation tokens for async operations
* Use global exception handling
* Use repository pattern
* Use Swagger/OpenAPI

## CQRS Rules

* Use MediatR
* Commands for write operations
* Queries for read operations
* Add FluentValidation validators for every command and query

## Security Standards

* JWT access tokens
* Refresh tokens
* BCrypt password hashing
* Role-based authorization

## Testing Standards

* xUnit
* Moq
* FluentAssertions

## Event Naming Convention

Use past tense event names:

* UserRegistered
* PostCreated
* PostLiked
* ConnectionRequested
* ConnectionAccepted
