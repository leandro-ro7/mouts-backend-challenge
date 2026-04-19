[Back to README](../README.md)

## Overview

This project serves as an evaluation for senior developer candidates. It assesses the skills and competencies required for a senior developer role, including:

1. Proficiency in C# and .NET 8.0 development
2. Domain-Driven Design (DDD) — aggregates, value objects, domain events, factory methods
3. CQRS with MediatR — commands, queries, handlers, pipeline validation behaviors
4. Transactional Outbox Pattern — at-least-once event delivery, crash recovery, poison-pill prevention
5. Optimistic concurrency control — `RowVersion` token, HTTP 409 on conflict
6. Database skills with PostgreSQL and EF Core
7. REST API design — versioning, pagination, idempotency, error contracts
8. Unit testing with xUnit, NSubstitute, and FluentAssertions (153 tests)
9. API versioning with `Asp.Versioning`
10. Error handling with structured responses and `traceId` correlation
11. Use of Git Flow and Semantic Commits
12. Clean architecture: Domain → Application → Infrastructure → ORM → WebApi → IoC
13. Asynchronous programming patterns
14. Code quality and adherence to best practices

### What Was Built

A fully functional **Sales Management API** (`/api/v1/sales`) implementing complete CRUD with domain-driven business rules:

- **Sale aggregate** — items, quantity-based discount tiers, soft-delete, `RowVersion` optimistic locking
- **Business rules** — 4–9 items: 10% discount; 10–20 items: 20% discount; >20: rejected
- **Domain events** published via Transactional Outbox: `SaleCreated`, `SaleModified` (with financial delta), `SaleCancelled`, `ItemCancelled`, `ItemAdded`, `ItemModified`
- **Idempotent POST** creation — optional client-supplied `idempotencyKey` prevents duplicate sales on retry
- **Paginated list** with full navigation metadata: `totalItems`, `currentPage`, `totalPages`, `hasNextPage`, `hasPreviousPage`
- **Structured error responses** with `traceId` for every exception type

<br/>
<div style="display: flex; justify-content: space-between;">
  <a href="../README.md">Previous: Read Me</a>
  <a href="./tech-stack.md">Next: Tech Stack</a>
</div>
