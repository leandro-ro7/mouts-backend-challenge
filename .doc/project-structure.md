[Back to README](../README.md)

## Project Structure

```text
root
├── src/
│   ├── Ambev.DeveloperEvaluation.Domain/          # Entities, value objects, events, repository interfaces
│   │   ├── Common/                                # AggregateRoot base, IDomainEvent (OccurredAt, Version)
│   │   ├── Entities/                              # Sale, SaleItem, User
│   │   ├── Enums/                                 # UserRole, UserStatus
│   │   ├── Events/                                # SaleCreatedEvent, SaleModifiedEvent, SaleCancelledEvent,
│   │   │                                          #   ItemCancelledEvent, ItemAddedEvent, UserRegisteredEvent
│   │   ├── Exceptions/                            # DomainException, ConcurrencyException
│   │   ├── Repositories/                          # ISaleRepository, IUserRepository
│   │   ├── Services/                              # IEventPublisher
│   │   └── ValueObjects/                          # DiscountRate, NewSaleItemSpec
│   │
│   ├── Ambev.DeveloperEvaluation.Application/     # CQRS commands, queries, handlers, validators
│   │   ├── Auth/AuthenticateUser/
│   │   └── Sales/
│   │       ├── CancelSaleItem/                    # PATCH /sales/{id}/items/{itemId}/cancel
│   │       ├── Common/                            # Shared DTOs (SaleItemResult, SaleSummaryResult)
│   │       ├── CreateSale/                        # POST /sales (with IdempotencyKey)
│   │       ├── DeleteSale/                        # DELETE /sales/{id} (soft-cancel, idempotent)
│   │       ├── GetSale/                           # GET /sales/{id}
│   │       ├── ListSales/                         # GET /sales (paginated, filtered)
│   │       └── UpdateSale/                        # PUT /sales/{id} (full replace with RowVersion)
│   │
│   ├── Ambev.DeveloperEvaluation.Infrastructure/  # Cross-cutting infrastructure
│   │   └── Messaging/
│   │       ├── LoggingEventPublisher.cs            # Default IEventPublisher (structured JSON log)
│   │       ├── OutboxProcessor.cs                  # BackgroundService: polls and dispatches OutboxMessages
│   │       ├── OutboxCleanupJob.cs                 # BackgroundService: purges old processed messages
│   │       └── OutboxOptions.cs                    # IOptions<T>: LockDurationSeconds, BatchSize, etc.
│   │
│   ├── Ambev.DeveloperEvaluation.ORM/             # EF Core DbContext, entity configurations, migrations
│   │   ├── DefaultContext.cs
│   │   ├── Mapping/                               # SaleConfiguration (incl. GIN trgm index),
│   │   │                                          #   SaleItemConfiguration, OutboxMessageConfiguration
│   │   ├── Migrations/
│   │   ├── Outbox/OutboxMessage.cs
│   │   └── Repositories/                          # SaleRepository, UserRepository
│   │
│   ├── Ambev.DeveloperEvaluation.Common/          # JWT token generator, shared utilities
│   │
│   ├── Ambev.DeveloperEvaluation.IoC/             # DI wiring (ModuleInitializers)
│   │
│   └── Ambev.DeveloperEvaluation.WebApi/          # ASP.NET Core host, controllers, middleware
│       ├── Features/Sales/                        # SalesController, request/response DTOs, AutoMapper profiles
│       ├── Middleware/ValidationExceptionMiddleware.cs
│       └── Program.cs
│
└── tests/
    ├── Ambev.DeveloperEvaluation.Unit/            # 153 unit tests (domain, application, infrastructure, WebApi)
    │   ├── Application/Sales/                     # Handler and validator tests
    │   ├── Domain/Entities/Sale/                  # Aggregate behaviour and discount rules
    │   ├── Infrastructure/                        # OutboxProcessor (9 scenarios)
    │   └── WebApi/                                # ValidationExceptionMiddleware (8 scenarios)
    │
    ├── Ambev.DeveloperEvaluation.Functional/      # 14 end-to-end HTTP tests against in-memory host
    │   └── Sales/SalesApiTests.cs
    │
    └── Ambev.DeveloperEvaluation.Integration/     # Integration tests against real PostgreSQL (Testcontainers)
        ├── Infrastructure/
        │   ├── PostgreSqlFixture.cs               # Spins up postgres:16-alpine container, runs migrations
        │   └── PostgreSqlCollection.cs            # xUnit [CollectionDefinition] for shared fixture
        └── Sales/
            ├── CreateSaleIntegrationTests.cs      # Round-trip, outbox write, DomainException rollback
            ├── SaleRepositoryPostgresTests.cs     # ILIKE search, partial unique index, RowVersion concurrency,
            │                                      #   pagination with AsSplitQuery
            └── OutboxProcessorPostgresTests.cs    # FOR UPDATE SKIP LOCKED, at-least-once, processed de-dup
```

[Previous: Auth API](./auth-api.md) | [Next: README](../README.md)
