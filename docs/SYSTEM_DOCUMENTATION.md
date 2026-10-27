# BuildConnect – System Documentation

Construction Professional Networking Platform · .NET 9 microservices · Angular frontend

Version 1.0 · October 2026

## 1. Introduction

BuildConnect is a professional network for the construction industry. Customers post construction jobs
(called **requirements**). Professionals such as masons, electricians and roofers find those jobs, express interest
and get hired. Everyone builds a network of **connections**, shares **posts**, and receives **notifications**.

The platform is built as independent microservices. Each service owns one business capability and its own
database. Services talk to each other through synchronous HTTP calls (when an answer is needed immediately) and
through Kafka events (when other services only need to know that something happened).

This document describes:

- the services and how they are deployed;
- every public API endpoint, with its parameters and rules;
- the business logic of each service;
- how the Neo4j graph database stores and queries the professional network;
- how the Notification service turns events into notifications;
- reliability, security, observability and testing.

## 2. System overview

### 2.1 Services

| Service | Responsibility | Data store | Local port / path |
|---|---|---|---|
| GatewayService | Single entry point; routes `/api/*` to services (YARP reverse proxy); CORS | none | http://localhost:5088 |
| IdentityService | Registration, login, JWT access tokens, rotating refresh tokens | PostgreSQL `IdentityDb` | `/api/auth` |
| UserService | Profiles (customer / professional), trade, skills, location, availability, search | PostgreSQL `linkedin_users` | `/api/users`, `/api/skills` |
| RequirementService | Jobs, matching, recommendations, interest, hiring lifecycle | PostgreSQL `requirements` | `/api/requirements` |
| ConnectionService | Connection requests, connections, mutual contacts, suggestions, network degree | Neo4j (`LinkedInDB`) | `/api/connections` |
| PostService | Posts, comments, likes, network feed | PostgreSQL `linkedin_posts` | `/api/posts`, `/api/comments` |
| NotificationService | Consumes Kafka events; stores and serves in-app notifications | SQL Server `linkedin_notifications` | `/api/notifications` |
| BuildingBlocks | Shared library: JWT validation, error handling, Kafka publisher, outbox, tracing, health checks | – | – |

`ProductService` and `OrderService` are legacy projects kept in the solution. They are not part of BuildConnect
and nothing depends on them.

Supporting infrastructure: Apache Kafka (event bus), Jaeger (distributed tracing, http://localhost:16686),
Seq (structured logs, http://localhost:5380), Angular web app (http://localhost:4200).

### 2.2 Architecture diagram

```
                     Angular web app (browser)
                               |
                               v
               API Gateway (YARP)  - forwards the JWT
                               |
   +-----------+-----------+---+--------+-----------+--------------+
   |           |           |            |           |              |
Identity     User     Requirement  Connection     Post       Notification
   |           |           |            |           |              |
PostgreSQL PostgreSQL  PostgreSQL    Neo4j      PostgreSQL     SQL Server
                           |            |           |              ^
                           +------------+-----------+              |
                            outbox -> Kafka topics ----------------+
```

### 2.3 Internal structure of a service

Every service follows the same layering:

```
Controllers/      HTTP endpoints: read the user id from the JWT, send a MediatR command or query
Application/      commands, queries, validators (FluentValidation), handlers, DTOs, interfaces
Domain/           entities and business rules (e.g. Requirement status transitions, ConnectionRules)
Infrastructure/   EF Core / Neo4j repositories, HTTP clients, Kafka relays and consumers
```

- **CQRS with MediatR:** each write is a command and each read is a query, each with its own handler.
- **Validation pipeline:** a MediatR behaviour runs the FluentValidation validator before every handler; failures
  return HTTP 400 with the list of errors.
- **Repository pattern:** handlers depend on interfaces; EF Core (PostgreSQL / SQL Server) or the Neo4j driver
  implements them.

## 3. API conventions

### 3.1 Base URL and authentication

- All requests go through the gateway: `http://localhost:5088/api/...`.
- Protected endpoints need the header `Authorization: Bearer <accessToken>`.
- The signed-in user's id is always taken from the token, never from the request body.
- Swagger UI: `http://localhost:5088/swagger`.

### 3.2 Response envelope

Every response uses the same JSON envelope (`ApiResponse<T>`):

```
{
  "success": true,
  "message": "Requirement created.",
  "data": { ... },
  "errors": null,
  "timestamp": "2026-10-10T12:00:00Z",
  "correlationId": "...",
  "statusCode": 200
}
```

### 3.3 Status codes

| Code | Meaning | Typical cause |
|---|---|---|
| 200 / 201 / 204 | Success | – |
| 400 | Validation failed | Missing or too long field; `errors` lists each problem |
| 401 | Not authenticated | Missing, expired or forged access token |
| 403 | Forbidden | Wrong role (e.g. a professional posting a job) or not the owner |
| 404 | Not found | Unknown id, deleted post, no pending request |
| 409 | Conflict | Business rule violated (e.g. job not OPEN, already connected, already responded) |
| 503 | Dependency unavailable | Registration while UserService is down |
| 500 | Unexpected error | Returned without internal details; logged with a correlation id |

### 3.4 Paging

List endpoints that can grow use `page` (from 1) and `pageSize` (1–50, default 20).

## 4. API reference

### 4.1 Identity service – `/api/auth`

| Method | Path | Auth | Body / parameters | Description |
|---|---|---|---|---|
| POST | `/api/auth/register` | none | `email`, `password` (min 6), `firstName`, `lastName`, `userType` (CUSTOMER / PROFESSIONAL), `professionalType`, `experienceYears`, `location` | Creates the account and its profile in UserService. 409 if the email exists; 503 (and no account) if UserService is unavailable. |
| POST | `/api/auth/login` | none | `email`, `password` | Returns `accessToken` (JWT, 15 min) and `refreshToken` (7 days). 401 on wrong credentials. |
| POST | `/api/auth/refresh-token` | none | `refreshToken` | Returns a new access token and a new refresh token; the old refresh token stops working. 401 if expired or reused. |
| POST | `/api/auth/logout` | none | `refreshToken` | Revokes the refresh token. Always succeeds. |
| GET | `/api/auth/profile` | JWT | – | Returns the claims of the current token (debugging aid). |

### 4.2 User service – `/api/users`, `/api/skills`

| Method | Path | Auth | Body / parameters | Description |
|---|---|---|---|---|
| GET | `/api/users/me` | JWT | – | My profile. |
| POST | `/api/users/me` | JWT | `firstName`, `lastName`, `displayName`, `headline`, `about`, `location`, `userType`, `professionalType`, `experienceYears`, `availabilityStatus` | Creates my profile when it is missing (profile setup screen). |
| PUT | `/api/users/me` | JWT | same fields as above except `userType`, plus `profileImageUrl` | Updates my profile. The user type cannot change. |
| GET | `/api/users/{userId}` | JWT | – | Another user's profile. |
| GET | `/api/users/search` | JWT (customer only) | query: `term`, `professionalType`, `location`, `skill`, `minimumExperienceYears`, `availabilityStatus` | Finds professionals. 403 for professionals. |
| POST | `/api/users/batch` | JWT | list of user ids | Display names and summaries for many users at once (used by other services). |
| GET | `/api/skills` | none | – | Skill catalogue. |
| POST | `/api/users/{userId}/skills` | JWT (self, professional) | `skillId`, `experienceYears`, `skillLevel` (BEGINNER / INTERMEDIATE / ADVANCED / EXPERT) | Adds a skill to my professional profile. |
| DELETE | `/api/users/{userId}/skills/{skillId}` | JWT (self, professional) | – | Removes a skill from my profile. |
| POST | `/internal/users/profiles` | service key | profile fields | Internal: called by IdentityService during registration. Not routed by the gateway. |

### 4.3 Requirement service – `/api/requirements`

| Method | Path | Auth | Body / parameters | Description |
|---|---|---|---|---|
| POST | `/api/requirements` | JWT (customer) | `title` (≤200), `description` (≤4000), `professionalType`, `requiredSkillIds` (≤20), `location` (≤120), `startDate` (today or later), `expectedDuration` (≤100) | Posts a job (status OPEN) and notifies the best-matching professionals. Returns 201. |
| GET | `/api/requirements` | none | query: `professionalType`, `location`, `status`, `mine` (customer: only my jobs) | Search jobs. Responses are visible only to the owner. |
| GET | `/api/requirements/{id}` | none | – | Job details. |
| GET | `/api/requirements/recommended` | JWT (professional) | – | Open jobs of my trade with a match score ≥ 60, best first. |
| GET | `/api/requirements/responses/me` | JWT (professional) | – | Jobs I have expressed interest in, with my response status. |
| PUT | `/api/requirements/{id}` | JWT (owner) | same fields as create | Edits an OPEN job. The trade cannot change once someone has responded. |
| DELETE | `/api/requirements/{id}` | JWT (owner) | – | Cancels an OPEN or IN_PROGRESS job; interested and hired professionals are notified. 204. |
| POST | `/api/requirements/{id}/complete` | JWT (owner) | – | Marks an IN_PROGRESS job COMPLETED. |
| POST | `/api/requirements/{id}/interest` | JWT (professional of the same trade) | `message` (optional, ≤1000) | Expresses interest in an OPEN job; the customer is notified. |
| GET | `/api/requirements/{id}/responses` | JWT (owner) | – | Professionals who responded, with names and status. |
| PUT | `/api/requirements/{id}/responses/{responseId}` | JWT (owner) | `status`: ACCEPTED or REJECTED | ACCEPTED hires that professional (job becomes IN_PROGRESS, all other pending responses are rejected). REJECTED declines one response. Each affected professional is notified. |

### 4.4 Connection service – `/api/connections`

All endpoints require a JWT.

| Method | Path | Body / parameters | Description |
|---|---|---|---|
| POST | `/api/connections/request?addresseeUserId={id}` | – | Sends a connection request; the addressee is notified. |
| POST | `/api/connections/accept` | `requesterUserId` | Accepts a pending request sent to me; the requester is notified. |
| POST | `/api/connections/reject` | `requesterUserId` | Declines a pending request sent to me (the requester is not told). |
| GET | `/api/connections/requests` | – | Pending requests I received. |
| GET | `/api/connections/sent-requests` | – | Pending requests I sent. |
| DELETE | `/api/connections/sent-requests/{addresseeUserId}` | – | Withdraws my pending request. |
| GET | `/api/connections/me` | – | My connections (newest first). |
| GET | `/api/connections/{userId}` | – | Another user's connections. |
| DELETE | `/api/connections/{otherUserId}` | – | Removes a connection. |
| GET | `/api/connections/status/{otherUserId}` | – | Relationship with a user: `SELF`, `NONE`, `PENDING_SENT`, `PENDING_RECEIVED` or `CONNECTED`, plus mutual and total connection counts (profile page button). |
| GET | `/api/connections/mutual/{otherUserId}` | – | Connections we share. |
| GET | `/api/connections/suggestions?limit=10` | `limit` 1–50 | "People you may know": friends of friends ranked by mutual connections. |
| POST | `/api/connections/degrees` | `userIds` (≤200) | Network distance (1 = connected, 2 = shared connection) from me to each user; used by RequirementService for the matching bonus. |

### 4.5 Post service – `/api/posts`, `/api/comments`

All endpoints require a JWT.

| Method | Path | Body / parameters | Description |
|---|---|---|---|
| POST | `/api/posts` | `content` (≤3000) | Creates a post. |
| GET | `/api/posts/feed` | `scope` = `network` (default) or `all`; `page`, `pageSize` | Network feed: my posts and my connections' posts. `all`: everyone's posts. Newest first, with like / comment counts and whether I liked each post. |
| GET | `/api/posts/user/{authorUserId}` | `page`, `pageSize` | Posts written by one user (profile page). |
| GET | `/api/posts/{id}` | – | One post. |
| PUT | `/api/posts/{id}` | `content`, `imageUrl` (optional), `visibility` (optional) | Edits my post. |
| DELETE | `/api/posts/{id}` | – | Soft-deletes my post (with its likes and comments). |
| POST | `/api/posts/{id}/like` | – | Likes a post (idempotent); the author is notified once. Returns the new like state. |
| DELETE | `/api/posts/{id}/like` | – | Removes my like (idempotent). |
| GET | `/api/posts/{postId}/comments` | – | Comments, each with `canEdit` / `canDelete` flags for the viewer. |
| POST | `/api/comments` | `postId`, `content` (≤1000) | Adds a comment; the post author is notified. |
| PUT | `/api/comments/{commentId}` | `content` | Edits my comment. |
| DELETE | `/api/comments/{commentId}` | – | Deletes a comment (comment author or post author). |

### 4.6 Notification service – `/api/notifications`

All endpoints require a JWT. Users only ever see their own notifications.

| Method | Path | Parameters | Description |
|---|---|---|---|
| GET | `/api/notifications` | `page`, `pageSize` | My notifications, newest first. |
| GET | `/api/notifications/unread-count` | – | Number of unread notifications (header badge). |
| PATCH | `/api/notifications/{id}/read` | – | Marks one notification as read. |
| PATCH | `/api/notifications/read-all` | – | Marks all as read; returns how many changed. |

### 4.7 Health endpoints (every service)

| Path | Purpose |
|---|---|
| `GET /health` | Liveness: the process is running. |
| `GET /health/ready` | Readiness: the service's database (or Neo4j) is reachable. |

## 5. Identity service

### 5.1 Registration

1. The request is validated (email format, password at least 6 characters, names, user type, trade for professionals).
2. IdentityService creates the account with a hashed password. 409 if the email exists.
3. IdentityService calls UserService's internal endpoint `/internal/users/profiles` (protected by a shared service
   key sent in the `X-Internal-Service-Key` header) to create the profile with the same user id.
4. If that call fails, the account is deleted again and the client gets 503 "try again". This prevents accounts
   without profiles.

### 5.2 Tokens

- **Access token:** JWT signed with a shared HMAC key, valid 15 minutes. It carries the user id and email. The
  gateway forwards it; every service validates it itself.
- **Refresh token:** a random value, valid 7 days, stored only as a SHA-256 hash.
- **Rotation:** each refresh returns a new pair and marks the old refresh token as replaced.
- **Reuse detection:** if an already used refresh token is presented again, it is treated as stolen and **all of that
  user's sessions are revoked**. The user must log in again.
- **Logout:** revokes the presented refresh token.

## 6. User service

- Holds the profile: first and last name, display name, headline, about, location, user type, professional type,
  experience years, availability, profile image and skills.
- **User types:** CUSTOMER or PROFESSIONAL. Fixed after creation.
- **Professional types:** MASON, CARPENTER, PLUMBER, ELECTRICIAN, PAINTER, TILE_WORKER, WELDER, ROOFER,
  CIVIL_ENGINEER, ARCHITECT, INTERIOR_DESIGNER, CONTRACTOR, CONSTRUCTION_SUPERVISOR, PROJECT_MANAGER, HVAC_TECHNICIAN,
  OTHER.
- **Availability:** AVAILABLE, BUSY, NOT_AVAILABLE.
- **Skills:** only professionals manage skills, and only on their own profile (otherwise 403). Each skill has
  experience years and a level.
- **Search:** customers can search professionals by name, trade, location, skill, minimum experience and
  availability ("Find Professionals" page).
- **Service-to-service role:** other services ask UserService for display names (batch), for a user's type, and
  for professionals' match profiles (trade, skills, location, availability, experience).

## 7. Requirement service

### 7.1 Requirement lifecycle

```
              customer posts
                    |
                    v
                 [OPEN] ----------- customer cancels -----------+
                    |                                           |
   customer accepts one response                                |
   (all other pending responses auto-rejected)                  |
                    |                                           v
                    v                                      [CANCELLED]
             [IN_PROGRESS] -------- customer cancels ---------->^
                    |
         customer completes
                    |
                    v
              [COMPLETED]
```

Response statuses: INTERESTED → ACCEPTED or REJECTED. A decision is final.

### 7.2 Business rules

All status changes go through methods of the `Requirement` entity, so no handler can skip a rule.

| Action | Who | Rule (violations return 403 / 409) |
|---|---|---|
| Post | Customers only | Start date must be today or later; at most 20 required skills |
| Edit | Owner | Only while OPEN; the trade cannot change after someone has responded |
| Express interest | Professionals of the requested trade | Only while OPEN; not on your own job; once per professional |
| See responses | Owner | Other users never see who responded |
| Accept a response | Owner | Only while OPEN; the response must still be INTERESTED. The job becomes IN_PROGRESS and every other pending response is rejected |
| Reject a response | Owner | Only while OPEN; the job stays OPEN |
| Cancel | Owner | Only OPEN or IN_PROGRESS; interested and hired professionals are notified |
| Complete | Owner | Only IN_PROGRESS |

**Concurrency:** the requirement row has an optimistic concurrency token (PostgreSQL `xmin`). If two decisions on
the same job are made at the same moment, only the first one is saved; the second gets 409.

### 7.3 Matching algorithm

`RequirementMatchingService` scores how well a professional fits a job, from 0 to 100.

| Component | Points | How it is computed |
|---|---|---|
| Same trade | 40 | Required: professionals of a different trade are not considered at all |
| Skills | 0–30 | 30 × (required skills the professional has ÷ required skills). 30 if the job lists no skills |
| Location | 0 / 8 / 15 | 15 if the locations are equal; 8 if one contains the other (e.g. "Pune" and "Kothrud, Pune"); otherwise 0 |
| Availability | 0 / 5 / 10 | AVAILABLE 10, BUSY 5, NOT_AVAILABLE 0 |
| Experience | 0 / 5 | 5 if the professional has any years of experience |
| Network trust bonus | +10 / +5 | +10 if customer and professional are connected (1st degree); +5 if they share a connection (2nd degree) |

The total is capped at 100.

**Worked example.** A job needs 2 skills in Pune. An available electrician in Pune with 1 of the 2 skills and 3
years of experience, directly connected to the customer, scores 40 + 15 + 15 + 10 + 5 + 10 = **95**.

**How the score is used:**

- **When a job is posted:** the service fetches professionals of that trade from UserService and their network
  degree from ConnectionService. It notifies the **top 5** with a score of **70 or more**.
- **Recommended jobs for a professional:** open jobs of their trade with a score of **60 or more**, best first.
- **Graceful degradation:** ConnectionService is called with a 3-second timeout. If it is unavailable, matching
  still runs without the network bonus instead of failing.

### 7.4 Events published

| Action | Topic | Who is notified |
|---|---|---|
| Job posted | `requirement-created` | Top matching professionals (score ≥ 70, at most 5) |
| Interest expressed | `professional-interest-submitted` | The customer |
| Response accepted / rejected (including auto-rejected) | `professional-interest-decision` | Each affected professional |
| Job cancelled | `requirement-cancelled` | Professionals still interested or hired |

## 8. Connection service and the Neo4j graph database

### 8.1 Why a graph database

Professional networks are about relationships: "friends of my friends", "connections we share", "is this
professional within two steps of the customer". In Neo4j these are short path patterns that start at one user and
follow relationships. In a relational database they become self-joins or recursive queries over one large table.
The benchmark in section 14 measures this trade-off.

### 8.2 Graph model

```
(:User {id})
   -[:REQUESTED {status: PENDING | REJECTED, createdAtUtc, respondedAtUtc}]->  (:User {id})
   -[:CONNECTED_WITH {requestedAtUtc, createdAtUtc}]-                          (:User {id})

(:OutboxMessage {id, topic, key, payload, occurredAtUtc, processedAtUtc, attempts, lastError, traceParent})
```

- **User nodes** store only the user id. Names and profiles belong to UserService and are fetched when needed.
- **REQUESTED** is directed (who asked whom) and carries the request status.
- **CONNECTED_WITH** is one relationship per pair, queried without direction, because a connection is mutual.
- **OutboxMessage** nodes hold events waiting to be published to Kafka (section 11).

**Schema** (created at startup by `Neo4jSchemaInitializer`):

```
CREATE CONSTRAINT user_id_unique   IF NOT EXISTS FOR (u:User) REQUIRE u.id IS UNIQUE
CREATE CONSTRAINT outbox_id_unique IF NOT EXISTS FOR (m:OutboxMessage) REQUIRE m.id IS UNIQUE
CREATE INDEX      outbox_pending   IF NOT EXISTS FOR (m:OutboxMessage) ON (m.processedAtUtc)
```

The unique constraint on `User.id` also acts as the index used to find the starting user of every query.

### 8.3 Connection workflow

```
            A sends request
                  |
                  v
             [PENDING] --- B declines ---> [REJECTED] (A is not told; A may ask again later)
              |      \
   B accepts  |       \--- A withdraws ---> (request deleted)
              v
          [CONNECTED] --- either user removes ---> (connection deleted)
```

**Rules for sending a request** (`ConnectionRules`, unit tested):

- You cannot send a request to yourself (400).
- The addressee must exist in UserService (404).
- Already connected → 409.
- You already have a pending request to them → 409.
- They already sent you a pending request → 409 ("accept it from your connection requests").
- A previously REJECTED request may be sent again; it becomes PENDING.

Only the addressee can accept or reject: the query matches only a request that points at the signed-in user.

### 8.4 How each operation works in Cypher

**Send a request.** One write transaction does everything:

1. `MERGE` both User nodes (created on first use).
2. Lock both nodes (`SET a._lock = true, b._lock = true REMOVE ...`). Two opposite requests sent at the same moment
   (A→B and B→A) are serialised, so only one can succeed.
3. Read the current state: connected? my request status? their pending request?
4. Apply `ConnectionRules` in C#; a violation throws and rolls the transaction back.
5. `MERGE (a)-[r:REQUESTED]->(b) SET r.status = 'PENDING'` and create the outbox node for `connection-requested`.

**Accept.** In one transaction: match the pending `REQUESTED` from the requester to me, delete it (and any reverse
request), `MERGE (a)-[:CONNECTED_WITH]-(b)`, and create the outbox node for `connection-accepted`.

**Reject.** Set `status = 'REJECTED'` and `respondedAtUtc`. No event is published, so the requester is not told.

**People you may know (suggestions):**

```
MATCH (me:User {id: $me})-[:CONNECTED_WITH]-(friend:User)-[:CONNECTED_WITH]-(candidate:User)
WHERE candidate <> me
  AND NOT (me)-[:CONNECTED_WITH]-(candidate)
  AND NOT (me)-[:REQUESTED]-(candidate)
RETURN candidate.id AS userId, count(DISTINCT friend) AS mutual
ORDER BY mutual DESC, userId
LIMIT $limit
```

Friends of friends, excluding me, people I am already connected to, and anyone with a request in either direction
(including rejected ones, so a declined person is not suggested again). Ranked by the number of mutual connections.

**Mutual connections:**

```
MATCH (:User {id: $me})-[:CONNECTED_WITH]-(mutual:User)-[:CONNECTED_WITH]-(:User {id: $other})
WHERE mutual.id <> $me AND mutual.id <> $other
RETURN DISTINCT mutual.id AS userId
```

**Network degree** (used for the matching bonus):

```
MATCH (me:User {id: $me})
UNWIND $candidates AS candidateId
MATCH (candidate:User {id: candidateId})
WHERE candidate <> me
OPTIONAL MATCH (me)-[direct:CONNECTED_WITH]-(candidate)
OPTIONAL MATCH (me)-[:CONNECTED_WITH]-(mutual:User)-[:CONNECTED_WITH]-(candidate)
WHERE mutual <> me AND mutual <> candidate
WITH candidateId, count(DISTINCT direct) > 0 AS connected, count(DISTINCT mutual) AS mutualCount
WHERE connected OR mutualCount > 0
RETURN candidateId AS userId, CASE WHEN connected THEN 1 ELSE 2 END AS degree, mutualCount
```

One query answers the question for up to 200 candidates. Users further than two steps away are left out.

**Relationship status** (profile page button): one read query returns whether we are connected, whether a pending
request exists in either direction, the number of mutual connections and the other user's total connections.
The result is `CONNECTED`, `PENDING_SENT`, `PENDING_RECEIVED` or `NONE`.

### 8.5 Transactions

- Reads use `ExecuteReadAsync` (read transactions); writes use `ExecuteWriteAsync`.
- Every write that produces an event stores its `OutboxMessage` node in the **same transaction**, so the graph change
  and its event are saved together or not at all.

## 9. Post service

- **Create / edit / delete:** only the author edits or deletes a post. Delete is a **soft delete**: the post, its
  likes and comments disappear everywhere but stay in the database.
- **Feed:**
  - `network` (default): my posts and posts from my connections. The connection list comes from ConnectionService;
    PostService never reads the graph itself.
  - `all`: everyone's posts.
  - If ConnectionService is down, the network feed falls back to all posts.
- **Counts:** like and comment counts and "liked by me" are computed in one database query. Author names come from
  UserService in one batch call per page.
- **Likes:** idempotent. Liking twice or unliking a post you never liked returns the current state. A unique key
  prevents duplicate likes even with double clicks. The author is notified once per liker, and never about liking
  their own post.
- **Comments:** the comment author can edit; the comment author or the post author can delete (the post author
  moderates their own discussion). The API returns `canEdit` / `canDelete` flags so the UI never guesses.
- **Events:** `post-liked`, `post-commented`.

## 10. Notification service

### 10.1 Role

NotificationService is the only consumer of business events. It never calls other services and never decides
whether an action was allowed; it only turns facts that already happened into messages for users.

### 10.2 Kafka consumer

- Subscribes to 8 topics with its own consumer group.
- `AutoOffsetReset = Earliest` and **manual commit** (`EnableAutoCommit = false`): the offset is committed only after
  the notifications are stored, so a crash never loses an event.
- **Temporary failure** (e.g. SQL Server down): the message is retried with backoff from 2 s up to 60 s, without
  committing the offset, so nothing is lost and order is kept.
- **Permanent failure** (invalid JSON or invalid data): the message is published to the dead-letter topic
  `notification-events-dlq` with the error, the offset is committed, and the consumer moves on.
- **Tracing:** each message's trace context is read from its Kafka headers, so the notification appears in the same
  Jaeger trace as the request that caused it.

### 10.3 Event-to-notification mapping

`NotificationEventMapper` is a pure function (no I/O, unit tested) that returns one notification per recipient.

| Topic | Recipient | Type | Title | Reference |
|---|---|---|---|---|
| `requirement-created` | Each matched professional | NewRequirementMatch | New Requirement Matching Your Profile (includes match score) | Requirement |
| `requirement-cancelled` | Interested / hired professionals | RequirementCancelled | Requirement Cancelled | Requirement |
| `professional-interest-submitted` | Customer | ProfessionalInterested | New Professional Interest | Requirement |
| `professional-interest-decision` | Professional | InterestAccepted / InterestRejected | You Have Been Selected / Not Selected | Requirement |
| `connection-requested` | Addressee | ConnectionRequested | New Connection Request | User (requester) |
| `connection-accepted` | Requester | ConnectionAccepted | Connection Accepted | User (addressee) |
| `post-liked` | Post author | PostLiked | Your Post Was Liked | Post |
| `post-commented` | Post author | PostCommented | New Comment On Your Post | Post |

The reference (type and id) lets the UI open the right page when a notification is clicked: the requirement, the
other user's profile, or the post. Messages are truncated to 500 characters.

### 10.4 Data model and idempotency

`Notification`: `Id`, `RecipientUserId`, `EventId`, `Type`, `Title`, `Message`, `ReferenceId`, `ReferenceType`,
`IsRead`, `CreatedAtUtc`.

| Index | Purpose |
|---|---|
| `(RecipientUserId, CreatedAtUtc)` | Fast "my notifications, newest first" |
| `(RecipientUserId, IsRead)` | Fast unread count |
| `(EventId, RecipientUserId)` **unique** | Idempotency: an event delivered twice cannot create a second notification |

## 11. Event-driven messaging

### 11.1 Why Kafka

Kafka keeps events in a durable, replayable log with ordering per key. A new consumer can read past events, and
consumers can be down without losing anything. Only one broker is used in the system.

### 11.2 Topics

| Topic | Producer |
|---|---|
| `requirement-created`, `requirement-cancelled`, `professional-interest-submitted`, `professional-interest-decision` | RequirementService |
| `connection-requested`, `connection-accepted` | ConnectionService |
| `post-liked`, `post-commented` | PostService |
| `notification-events-dlq` | NotificationService (dead letters) |

Event contracts are in `BuildingBlocks/Events`; topic names are in `EventTopics`. Every event has a unique
`EventId`.

### 11.3 Transactional outbox

Saving to the database and publishing to Kafka are two systems, so one can succeed while the other fails (the
"dual write" problem). The outbox solves it:

```
 Handler                       Database (one transaction)          Background relay              Kafka
    |  save business change  ->  business rows                          |                         |
    |  save event            ->  OutboxMessages row / node              |                         |
    |  commit                                                           |                         |
    |                                   every 2 s: read pending (50) -> |  publish in order  ->    |
    |                                   mark processed  <-------------- |                         |
```

- PostgreSQL services use an `OutboxMessages` table (shared EF Core implementation in BuildingBlocks).
- ConnectionService uses `(:OutboxMessage)` nodes in Neo4j.
- The relay polls every 2 seconds, publishes up to 50 messages in order, and backs off exponentially to 60 seconds
  while Kafka is unreachable. It never skips a message.
- Result: **at-least-once delivery**. If a change was saved, its event is eventually published, even if Kafka was
  down at that moment. Duplicates are handled by the idempotent consumer.
- The W3C trace context is stored with each event and sent as a Kafka header.

### 11.4 Synchronous calls between services

| Caller → callee | Purpose | If the callee is down |
|---|---|---|
| Identity → User | Create the profile at registration | Registration rolled back; 503 |
| Requirement → User | Is the caller a customer / professional; professionals' match profiles | Request fails |
| Requirement → Connection | Network degrees for the matching bonus (3 s timeout) | Matching without the bonus |
| Connection → User | Check the addressee exists; display names | Names fall back to a placeholder |
| Post → Connection | My connections for the network feed | Feed shows all posts |
| Post → User | Author names (batch) | Names fall back to a placeholder |

Calls made on behalf of a user forward that user's JWT. The registration call from Identity to User is made before
the user has a token, so it uses the `X-Internal-Service-Key` header instead.

## 12. End-to-end workflows

### 12.1 Posting a job

1. The customer submits the form; the request reaches RequirementService through the gateway.
2. RequirementService checks with UserService that the caller is a customer (otherwise 403).
3. It validates the request, loads professionals of that trade from UserService and their network degrees from
   ConnectionService.
4. It scores each professional, keeps the top 5 with a score of 70 or more, and saves the requirement and a
   `requirement-created` outbox event in one transaction. The customer gets 201 immediately.
5. The relay publishes the event to Kafka; NotificationService stores one notification per matched professional.
   End-to-end latency is about 2–3 seconds, mostly the relay's polling interval.

### 12.2 Hiring

1. A professional of the right trade clicks "I'm interested"; the customer is notified.
2. The customer opens the responses and accepts one. The job becomes IN_PROGRESS and the other pending responses
   are rejected in the same transaction.
3. Every affected professional gets "You Have Been Selected" or "Not Selected".
4. When the work is done the customer marks the job COMPLETED.

### 12.3 Building the network

1. User A opens User B's profile (from the professional directory, a post author, a job response, a notification or
   the suggestions list). The profile shows a **Connect** button based on `/api/connections/status/{id}`.
2. A clicks Connect: a `REQUESTED` relationship is created in Neo4j, and B is notified.
3. B accepts from "Connection Requests": the request becomes a `CONNECTED_WITH` relationship, and A is notified.
4. From then on, suggestions show friends of friends, the network feed shows each other's posts, and job matching
   adds the network trust bonus.

## 13. Security

- **Authentication:** JWT access tokens (15 min) validated by every service; refresh tokens rotated on every use,
  stored as SHA-256 hashes, with reuse detection.
- **Authorization:**
  - roles (customers post jobs and search professionals; professionals respond and manage skills);
  - ownership (only the job owner decides; only the addressee accepts; only the author edits a post or comment;
    users see only their own notifications).
- **Identity from the token:** user ids are never taken from request bodies.
- **Internal endpoint:** `/internal/users/profiles` is outside `/api`, so the gateway does not route it, and it
  requires a shared service key.
- **Secrets:** not in source control. .NET user-secrets locally (`scripts/setup-dev-secrets.ps1`), `.env` for
  Docker Compose (`scripts/new-docker-env.ps1`). Services refuse to start without a strong JWT key.
- **Error responses:** unexpected errors return 500 without stack traces or internal details.

## 14. Observability, deployment and evaluation

### 14.1 Observability

- **Tracing:** OpenTelemetry on every service (incoming and outgoing HTTP, Kafka). One trace in Jaeger shows a
  request from the browser, through the gateway and services, through the outbox and Kafka, to the notification.
- **Logs:** Serilog structured logs to the console and Seq, each with the service name and correlation id.
- **Health checks:** `/health` and `/health/ready` on every service.

### 14.2 Deployment

- `docker-compose.yml` runs the full platform: services, gateway, PostgreSQL, Neo4j, SQL Server, Kafka, Jaeger,
  Seq.
- Database migrations are applied automatically at startup.
- GitHub Actions builds the solution, runs the tests and builds all service images on every push.
- Local development steps are in `RUNNING.md`.

### 14.3 Testing and evaluation (details in `docs/EVALUATION.md`)

| Evaluation | Result |
|---|---|
| Unit tests (xUnit) | 104 / 104 passed |
| End-to-end tests through the gateway (real databases, Kafka, Neo4j) | 80 / 80 checks passed (identity 16, requirements 26, connections 20, posts 18) |
| Load test, 50 parallel clients | 316–1,671 requests/s per endpoint; 0 errors in about 162,000 requests |
| Neo4j vs PostgreSQL, 1–2 hop queries | PostgreSQL 1.4–6× faster (both under 22 ms p95) |
| Neo4j vs PostgreSQL, 3-hop queries | Neo4j 3–5× faster, with much lower tail latency (p95 52 ms vs 195 ms at 50,000 users) |

## Appendix A. Enumerations

| Name | Values |
|---|---|
| UserType | CUSTOMER, PROFESSIONAL |
| ProfessionalType | MASON, CARPENTER, PLUMBER, ELECTRICIAN, PAINTER, TILE_WORKER, WELDER, ROOFER, CIVIL_ENGINEER, ARCHITECT, INTERIOR_DESIGNER, CONTRACTOR, CONSTRUCTION_SUPERVISOR, PROJECT_MANAGER, HVAC_TECHNICIAN, OTHER |
| AvailabilityStatus | AVAILABLE, BUSY, NOT_AVAILABLE |
| SkillLevel | BEGINNER, INTERMEDIATE, ADVANCED, EXPERT |
| RequirementStatus | OPEN, IN_PROGRESS, COMPLETED, CANCELLED |
| ResponseStatus | INTERESTED, ACCEPTED, REJECTED |
| Connection request status | PENDING, REJECTED |
| Relationship status | SELF, NONE, PENDING_SENT, PENDING_RECEIVED, CONNECTED |

## Appendix B. Technology stack

| Area | Technology |
|---|---|
| Backend | .NET 9, ASP.NET Core, MediatR, FluentValidation, EF Core |
| Gateway | YARP reverse proxy |
| Databases | PostgreSQL, Neo4j 5, SQL Server 2022 |
| Messaging | Apache Kafka (Confluent.Kafka client) |
| Frontend | Angular |
| Observability | OpenTelemetry, Jaeger, Serilog, Seq |
| Delivery | Docker Compose, GitHub Actions |
| Testing | xUnit, Node.js end-to-end and load scripts |
