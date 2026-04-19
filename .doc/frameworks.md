[Back to README](../README.md)

## Frameworks

Our frameworks are the building blocks that enable robust, efficient, and maintainable software solutions. Each was chosen for its ability to integrate with the tech stack, its community support, and alignment with DDD and clean architecture principles.

### Backend

- **MediatR** — CQRS mediator. Decouples command/query dispatch from handlers via in-process messaging. Also hosts the `ValidationBehavior` pipeline that runs FluentValidation before every handler.
  - [GitHub](https://github.com/jbogard/MediatR)
- **FluentValidation** — Declarative input validation. Rules are defined per command/query and executed automatically by the MediatR pipeline behavior before any handler runs.
  - [GitHub](https://github.com/FluentValidation/FluentValidation)
- **AutoMapper** — Convention-based object-to-object mapper. Used to map domain entities to DTOs (results) and API request objects to application commands.
  - [GitHub](https://github.com/AutoMapper/AutoMapper)
- **Asp.Versioning** — API versioning support for ASP.NET Core. All Sales endpoints are served under `/api/v1/`. New versions can be introduced without breaking existing consumers.
  - [GitHub](https://github.com/dotnet/aspnet-api-versioning)

### Messaging / Event Publishing

- **LoggingEventPublisher** *(default)* — Logs domain events as structured JSON via `ILogger`. Honest default: no broker dependency, events are visible in application logs. Registered as `IEventPublisher` in the DI container.
- **IEventPublisher** *(interface)* — Swap the default implementation by registering a different `IEventPublisher` in `InfrastructureModuleInitializer`. Any message broker (RabbitMQ, Azure Service Bus, Kafka) can be wired here without changes to the domain or application layers.

### Transactional Outbox

Domain events are written to the `OutboxMessages` table in the same database transaction as the aggregate mutation. A background service (`OutboxProcessor`) polls and dispatches them:

- **PostgreSQL path** — `FOR UPDATE SKIP LOCKED` ensures exactly one processor instance claims each batch. `ClaimId` per invocation prevents cross-instance fetch confusion.
- **InMemory path** — Tracked-entity fallback for tests. Safe because InMemory is always single-instance.
- **Crash recovery** — `LockedUntil` expiry re-exposes abandoned messages to the next poll.
- **Poison pill prevention** — `JsonException` on deserialization sets `ProcessedAt` permanently; the message is never retried.
- **Retention** — `OutboxCleanupJob` purges processed messages older than `RetentionDays` (default: 7 days).

### Event Versioning

Every `IDomainEvent` carries a `Version` field (currently `1` for all events). When a new non-optional field is added to an event, `Version` is incremented so the `OutboxProcessor` can route old messages in the queue to the appropriate deserializer instead of silently discarding data. `System.Text.Json` ignores unknown properties on deserialization by default, so additive changes (new optional fields) are forward-compatible without a version bump.

### Testing

- **xUnit** — Test runner and framework.
  - [GitHub](https://github.com/xunit/xunit)
- **NSubstitute** — Creates test doubles (mocks/stubs) without verbose setup. Preferred over Moq for its readable API.
  - [GitHub](https://github.com/nsubstitute/NSubstitute)
- **FluentAssertions** — Readable assertion syntax (`result.Should().Be(...)`). Produces detailed failure messages.
  - [GitHub](https://github.com/fluentassertions/fluentassertions)
- **Bogus (Faker)** — Generates realistic fake data for test scenarios.
  - [GitHub](https://github.com/bchavez/Bogus)
- **Testcontainers.PostgreSql** — Spins up a real PostgreSQL container for integration tests. Validates behaviors that only manifest against a real database: `FOR UPDATE SKIP LOCKED`, the partial unique index on `IdempotencyKey`, `IsConcurrencyToken` with Npgsql, and `AsSplitQuery` pagination.
  - [GitHub](https://github.com/testcontainers/testcontainers-dotnet)

### Database / ORM

- **EF Core** — Lightweight, extensible ORM. Used for all data access, migrations, and optimistic concurrency (`IsConcurrencyToken`).
  - [GitHub](https://github.com/dotnet/efcore)
- **EF Core InMemory** — In-process provider used in unit and functional tests. No external process or Docker required.
- **Npgsql.EntityFrameworkCore.PostgreSQL** — PostgreSQL provider for EF Core. Enables `EF.Functions.ILike` for case-insensitive search and GIN trigram indexes via `gin_trgm_ops`.
  - [GitHub](https://github.com/npgsql/efcore.pg)

[Previous: Tech Stack](./tech-stack.md) | [Next: General API](./general-api.md)
