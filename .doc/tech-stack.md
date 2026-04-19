[Back to README](../README.md)

## Tech Stack

The Tech Stack is a set of technologies used to build this project. All versions were chosen based on compatibility, community support, and alignment with .NET 8 LTS.

### Backend

- **.NET 8.0** (LTS) — Runtime and SDK.
  - [GitHub](https://github.com/dotnet/core)
- **C# 12** — Primary language.
  - [GitHub](https://github.com/dotnet/csharplang)
- **ASP.NET Core 8.0** — Web API host and middleware pipeline.
- **Entity Framework Core 8.0.10** — ORM, migrations, optimistic concurrency.
  - [GitHub](https://github.com/dotnet/efcore)
- **Npgsql.EntityFrameworkCore.PostgreSQL 8.0.8** — PostgreSQL provider for EF Core.
  - [GitHub](https://github.com/npgsql/efcore.pg)
- **MediatR 12.4.1** — CQRS mediator (commands, queries, pipeline behaviors).
  - [GitHub](https://github.com/jbogard/MediatR)
- **FluentValidation 11.10.0** — Input validation via MediatR pipeline behavior.
  - [GitHub](https://github.com/FluentValidation/FluentValidation)
- **AutoMapper 13.0.1** — Object mapping between DTOs and domain objects.
  - [GitHub](https://github.com/AutoMapper/AutoMapper)
- **Asp.Versioning.Mvc 8.1.0** — API versioning (`/api/v1/`).
  - [GitHub](https://github.com/dotnet/aspnet-api-versioning)
- **Rebus 8.4.1** — Message bus reference implementation (not active by default; swap in for real broker integration).
  - [GitHub](https://github.com/rebus-org/Rebus)

### Testing

- **xUnit 2.9.2** — Unit test framework.
  - [GitHub](https://github.com/xunit/xunit)
- **NSubstitute 5.1.0** — Mocking / test doubles.
  - [GitHub](https://github.com/nsubstitute/NSubstitute)
- **FluentAssertions 6.12.0** — Expressive assertion library.
  - [GitHub](https://github.com/fluentassertions/fluentassertions)
- **Bogus 35.6.1** — Fake data generation for tests.
  - [GitHub](https://github.com/bchavez/Bogus)
- **Microsoft.EntityFrameworkCore.InMemory 8.0.10** — In-process DB for infrastructure tests (no Docker required).

### Database

- **PostgreSQL 15+** — Primary production database.
  - [GitHub](https://github.com/postgres/postgres)
- **EF Core InMemory** — Unit and integration tests.

> MongoDB and Angular are **not** part of this implementation. The backend is a pure REST API.

<div style="display: flex; justify-content: space-between;">
  <a href="./overview.md">Previous: Overview</a>
  <a href="./frameworks.md">Next: Frameworks</a>
</div>
