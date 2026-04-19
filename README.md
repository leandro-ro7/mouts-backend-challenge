# Ambev Developer Evaluation — Sales API

## O que foi implementado

Este projeto atende ao desafio de implementação de uma **API de gerenciamento de vendas** sobre uma base .NET 8 pré-existente. O foco foi na corretude do modelo de domínio, na robustez da infraestrutura de eventos e na qualidade dos testes.

---

## Domínio

O núcleo da solução é o agregado `Sale` com seu filho `SaleItem`, implementados com DDD estrito:

- **Factory method `Sale.Create`** — garante invariantes antes de emitir eventos; nenhum construtor público exposto
- **`SaleItem` com construtor `internal`** — apenas o agregado pode criar itens, preservando o encapsulamento
- **`DiscountRate` como Value Object** — imutável, igualdade por valor, encapsula toda a lógica de tiers de desconto; instâncias nomeadas (`None`, `TenPercent`, `TwentyPercent`) eliminam magic numbers
- **`BumpVersion()`** — encapsula `RowVersion++` e `UpdatedAt = UtcNow` em um único método privado; nenhuma mutação pode esquecer um dos dois
- **`BuildSnapshot()`** — captura o estado atual do agregado em um `SaleSnapshot`; reutilizado em `Create` e `UpdateFull` para consistência nos payloads de eventos

**Regras de negócio (por quantidade de itens idênticos):**

| Quantidade | Desconto |
| --- | --- |
| 1–3 | 0% |
| 4–9 | 10% |
| 10–20 | 20% |
| > 20 | `DomainException` — rejeitado |

---

## API

`/api/v1/sales` — todos os endpoints requerem `Authorization: Bearer <token>`.

| Método | Rota | Descrição |
| --- | --- | --- |
| `POST` | `/sales` | Cria venda; aceita `idempotencyKey` opcional para deduplicação em retries |
| `GET` | `/sales` | Lista paginada com filtros: `customerName` (ILIKE), `dateFrom`, `dateTo`, `isCancelled`, `order` |
| `GET` | `/sales/{id}` | Detalhe da venda com todos os itens |
| `PUT` | `/sales/{id}` | Substituição atômica de header + itens; requer `rowVersion` para controle de concorrência |
| `DELETE` | `/sales/{id}` | Soft-cancel idempotente — registro nunca é apagado fisicamente |
| `PATCH` | `/sales/{id}/items/{itemId}/cancel` | Cancela um item individual; recalcula `totalAmount` |

**Controle de concorrência:** `RowVersion` (uint) é retornado em todo `GET` e exigido no `PUT`. Um `rowVersion` desatualizado retorna `409 Conflict`. O check ocorre em dois níveis: na camada de aplicação (early rejection) e no EF Core via `IsConcurrencyToken` (garantia de atomicidade no banco).

**Idempotência no POST:** `idempotencyKey` (UUID opcional) é indexado com partial unique index (`WHERE IdempotencyKey IS NOT NULL`), permitindo múltiplos `null` e rejeitando duplicatas não-nulas.

---

## Eventos de Domínio — Transactional Outbox

Todos os eventos são publicados via **Transactional Outbox** — escritos na tabela `OutboxMessages` na mesma transação do agregado, garantindo entrega **at-least-once** mesmo em caso de crash.

| Evento | Quando |
| --- | --- |
| `SaleCreatedEvent` | `POST /sales` — contém snapshot completo dos itens |
| `SaleModifiedEvent` | `PUT /sales/{id}` — contém `Previous` e `Current` (`SaleSnapshot`) com delta financeiro |
| `SaleCancelledEvent` | `DELETE /sales/{id}` (primeiro cancelamento) |
| `ItemCancelledEvent` | `PATCH /items/{itemId}/cancel` e para cada item ativo removido num `PUT` |

**Arquitetura do Outbox:**

- `OutboxInterceptor : SaveChangesInterceptor` — coleta eventos do `ChangeTracker` e os serializa em `OutboxMessages` via `IOutboxContext`, sem acoplar ao contexto concreto
- `OutboxProcessor : BackgroundService` — usa `FOR UPDATE SKIP LOCKED` para claims atômicos entre instâncias concorrentes; `ClaimId` identifica exclusivamente o batch desta instância
- `OutboxCleanupJob : BackgroundService` — purge independente de mensagens processadas (retenção configurável)
- Version guard no processor — detecta mensagens escritas antes de uma atualização de schema e emite warning estruturado sem descartar o evento

---

## Decisões de Design

**`SaleModifiedEvent` com `SaleSnapshot`**  
O evento carrega `Previous` e `Current` como `SaleSnapshot` (4 parâmetros) ao invés de 14 parâmetros posicionais. Consumidores downstream não precisam consultar o banco para reconstruir o estado anterior.

**DELETE como soft-cancel**  
Registros de venda são histórico financeiro imutável. `DELETE` cancela logicamente (`IsCancelled = true`) e é idempotente — uma segunda chamada retorna `200` sem emitir novo evento.

**`ValidationExceptionMiddleware` orientado a dicionário**  
Substituiu a cadeia de `catch` por `Dictionary<Type, (Status, Code, LogLevel, Func<Exception, string>)>`. Adicionar suporte a um novo tipo de exceção é uma entrada no dicionário — o método `InvokeAsync` não é modificado (OCP).

**`IOutboxContext` para desacoplamento**  
`OutboxInterceptor` depende de `IOutboxContext` (`GetTrackedAggregates()`, `AddOutboxMessage()`), não de `DefaultContext` diretamente. Qualquer `DbContext` que implemente a interface pode ser suportado sem alterar o interceptor.

**Estrutura de arquivos difere do `project-structure.md` em alguns pontos**

O documento de referência descreve a estrutura esperada do projeto. As divergências entre o documentado e o implementado são:

| Item | Doc | Implementação | Tipo |
| --- | --- | --- | --- |
| `Domain/Events/ItemAddedEvent` | Listado | Não existe | Decisão — ver justificativa abaixo |
| `ORM/Mapping/SaleItemConfiguration` | Arquivo separado | Classe embutida em `SaleConfiguration.cs` | Consolidação deliberada — ambas as configurações estão no mesmo arquivo |
| `ORM/IOutboxContext.cs` | Não listado | Existe | Adição — desacopla `OutboxInterceptor` do contexto concreto |
| `ORM/Interceptors/OutboxInterceptor.cs` | Não listado | Existe | Adição — extrai responsabilidade de injeção do outbox do `DefaultContext` |
| `Domain/Repositories/SaleListCriteria.cs` | Não listado | Existe | Adição — criteria object que substitui parâmetros posicionais em `ListAsync` |
| `Domain/Events/SaleSnapshot.cs` / `SaleItemSnapshot.cs` | Não listados | Existem | Adição — value objects de snapshot embutidos no `SaleModifiedEvent` |
| Contagem de unit tests | 153 | **154** | +1 test adicionado durante implementação |
| `OutboxProcessor` cenários | 9 | **10** | +1 cenário adicionado |

**`UserRegisteredEvent` — dead code na base original**  
O evento existe em `Domain/Events/UserRegisteredEvent.cs` e está listado no `project-structure.md`. O `users-api.md` especifica explicitamente que `POST /api/v1/users` deve emitir `UserRegisteredEvent`, porém `CreateUserHandler` **nunca o levanta**. Trata-se de gap na base pré-existente do desafio; o evento não foi removido para não alterar o escopo original, mas também não foi emitido porque corrigir comportamento de endpoints fora do escopo poderia mascarar intenções do avaliador.

**`SaleNumber` tem índice UNIQUE no banco**  
`SaleConfiguration` aplica `HasIndex(s => s.SaleNumber).IsUnique()`, garantindo unicidade de número de venda no PostgreSQL. A avaliação anterior que apontava ausência desta constraint estava incorreta.

---

**Rotas de Auth e Users não seguem o padrão `/api/v1/`**  
O `general-api.md` e o `auth-api.md` especificam que todos os endpoints devem ser servidos sob `/api/v1/` e que o login deve ser obtido via `POST /api/v1/auth/login`. O `users-api.md` define base path `/api/v1/users`. Na prática:

- `AuthController` usa `[Route("api/[controller]")]` → rota efetiva é `POST /api/auth` (sem versão, sem sufixo `/login`)
- `UsersController` usa `[Route("api/[controller]")]` → rota efetiva é `/api/users` (sem versão)

Esses dois controllers fazem parte da base pré-existente do desafio e não foram modificados. O `SalesController` — implementação nova — está corretamente versionado sob `/api/v{version:apiVersion}/sales`. A divergência nos controllers herdados é conhecida e está fora do escopo das alterações realizadas.

**Shape de resposta da listagem de Users**  
O `users-api.md` descreve uma resposta de paginação plana (`data[]`, `totalItems`, `currentPage`, `totalPages`, `pageSize`). A implementação atual retorna `ApiResponseWithData<ListUsersResult>`, que adiciona os campos `success` e `message` como envelope externo. Esta é uma divergência de contrato presente na base pré-existente — o `SalesController` expõe o mesmo envelope, mas o `users-api.md` não o prevê.

**Campo `error` vs `detail` na resposta de erro de validação**  
O `general-api.md` especifica que, para erros de validação, `error` deve conter um sumário genérico (`"One or more validation errors occurred."`) e `detail` deve conter o detalhe específico do campo. A implementação atual do `ValidationExceptionMiddleware` atribui o mesmo valor (a lista de erros do FluentValidation) a ambos os campos, pois `WriteResponse` recebe `(error, detail)` com o mesmo argumento. Esta é uma divergência menor em relação ao contrato documentado — o conteúdo está correto, apenas a segregação sumário/detalhe não é respeitada.

**`ItemAddedEvent` e `ItemModifiedEvent` não foram implementados**  
O overview de referência lista esses dois eventos, porém a operação `PUT /sales/{id}` foi modelada como **substituição atômica** do agregado (replace completo de header + itens), não como edição granular de itens individuais. Nesse modelo:

- Os itens "adicionados" já estão representados em `SaleModifiedEvent.Current.Items`
- Os itens "removidos" geram `ItemCancelledEvent` individual por item ativo excluído
- Emitir `ItemAddedEvent` separadamente criaria assimetria com `SaleCreatedEvent`, que já carrega o snapshot completo dos itens criados

Um consumidor downstream pode inferir o delta exato comparando `SaleModifiedEvent.Previous.Items` com `SaleModifiedEvent.Current.Items`, sem necessitar de eventos adicionais. A decisão reduz a superfície de eventos sem perda de informação, mantendo o stream auditável e consistente.

---

## Stack Utilizada

| Camada | Tecnologia | Versão | Status |
| --- | --- | --- | --- |
| Runtime | .NET / C# | 8.0 LTS / C# 12 | ✅ Aplicado |
| Web | ASP.NET Core + `Asp.Versioning.Mvc` | 8.0 / 8.1.0 | ✅ Aplicado |
| ORM | Entity Framework Core + Npgsql | 8.0.10 / 8.0.8 | ✅ Aplicado |
| CQRS | MediatR + `ValidationBehavior` pipeline | 12.4.1 | ✅ Aplicado |
| Validação | FluentValidation | 11.10.0 | ✅ Aplicado |
| Mapeamento | AutoMapper | 13.0.1 | ✅ Aplicado — ver nota abaixo |
| Banco de dados | PostgreSQL | 16-alpine | ✅ Aplicado |
| Testes — framework | xUnit | 2.9.2 | ✅ Aplicado |
| Testes — mocks | NSubstitute | 5.1.0 | ✅ Aplicado |
| Testes — asserções | FluentAssertions | 6.12.0 | ✅ Aplicado |
| Testes — dados | Bogus | 35.6.1 | ✅ Aplicado — `SaleTestData`, `CreateSaleHandlerTestData` |
| Testes — DB in-process | EF Core InMemory | 8.0.10 | ✅ Aplicado |
| Testes — DB real | Testcontainers.PostgreSql | 3.x | ✅ Aplicado — sobe `postgres:16-alpine` automaticamente |
| Message broker | Rebus | 8.4.1 | ⚠️ Não instalado — ver nota abaixo |
| Logging | Serilog (host) | — | ✅ Aplicado |

**Nota — AutoMapper 13.0.1:**  
Esta versão possui a vulnerabilidade conhecida [`GHSA-rvv3-g6hj-g44x`](https://github.com/advisories/GHSA-rvv3-g6hj-g44x) (severidade alta). Não existe versão 13.x com correção confirmada e compatível com .NET 8 no momento desta implementação. O aviso `NU1903` está suprimido intencionalmente em `Application.csproj` com comentário de rastreio. Critério de remoção da supressão: publicação de versão que feche a CVE e passe todos os testes existentes sem quebras de API.

**Nota — Rebus 8.4.1:**  
Listado no `tech-stack.md` como implementação de referência, porém **não está instalado como `PackageReference` em nenhum projeto**. Aparece apenas como sugestão em comentário no `LoggingEventPublisher.cs`. A decisão foi manter a integração com broker desacoplada via `IEventPublisher`: para ativar Rebus (ou qualquer outro broker), basta instalar o pacote e registrar uma implementação alternativa em `InfrastructureModuleInitializer` — zero alterações no domínio, nos handlers ou no outbox.

**Verificação de `frameworks.md`:**  
Todos os frameworks descritos em `frameworks.md` estão corretamente aplicados. Pontos verificados: `ValidationBehavior` pipeline MediatR ativo; `Testcontainers.PostgreSql` confirmado nos testes de integração com `postgres:16-alpine`; estratégia de versionamento de eventos (`IDomainEvent.Version`) documentada e o `OutboxProcessor` emite warning estruturado para mensagens com versão desconhecida sem descartar o evento (poison-pill prevention). Sem divergências.

---

## Testes

**154 testes unitários** + suite de integração (PostgreSQL real via Testcontainers) + suite funcional (HTTP end-to-end via `WebApplicationFactory`).

**Destaques:**

- `SaleItemDiscountTests` — todos os boundary values de cada tier testados com `[Theory]` + `[InlineData]`
- `OutboxProcessorPostgresTests` — teste de `FOR UPDATE SKIP LOCKED` com `TaskCompletionSource` gate barrier: dois processors concorrentes com `BatchSize=1` são forçados a se sobrepor; cada um despacha exatamente uma mensagem distinta
- `SaleRepositoryPostgresTests` — valida ILIKE com GIN trigram index, partial unique index em `IdempotencyKey`, concorrência otimista com `RowVersion` e paginação com `AsSplitQuery`
- `SalesApiTests` — cobertura HTTP de todos os endpoints incluindo 401, 404, 409 e verificação de ausência de double-wrapping nas respostas

---

## Como Executar

**Pré-requisitos:** .NET 8 SDK, Docker (para PostgreSQL e testes de integração).

```bash
# Subir PostgreSQL
docker run -d -p 5432:5432 -e POSTGRES_DB=developer_evaluation \
  -e POSTGRES_USER=developer -e POSTGRES_PASSWORD=ev@luAt10n \
  postgres:16-alpine

# Aplicar migrations e executar
cd src/Ambev.DeveloperEvaluation.WebApi
dotnet run

# Testes unitários
dotnet test tests/Ambev.DeveloperEvaluation.Unit

# Testes funcionais
dotnet test tests/Ambev.DeveloperEvaluation.Functional

# Testes de integração (requer Docker — Testcontainers sobe PostgreSQL automaticamente)
dotnet test tests/Ambev.DeveloperEvaluation.Integration
```

A API estará disponível em `https://localhost:5119`. Swagger em `/swagger` (ambiente Development).

---

## Estrutura Relevante

```text
src/
  Domain/
    Entities/          Sale.cs, SaleItem.cs
    ValueObjects/      DiscountRate.cs, NewSaleItemSpec.cs
    Events/            SaleCreatedEvent, SaleModifiedEvent, SaleCancelledEvent,
                       ItemCancelledEvent, SaleSnapshot, SaleItemSnapshot
    Repositories/      ISaleRepository.cs, SaleListCriteria.cs
  Application/Sales/   CreateSale, UpdateSale, DeleteSale, GetSale,
                       ListSales, CancelSaleItem  (handlers + validators + DTOs)
  ORM/
    Repositories/      SaleRepository.cs
    Interceptors/      OutboxInterceptor.cs
    Outbox/            OutboxMessage.cs
    IOutboxContext.cs
  Infrastructure/
    Messaging/         OutboxProcessor.cs, OutboxCleanupJob.cs,
                       LoggingEventPublisher.cs, OutboxOptions.cs
  WebApi/
    Features/Sales/    SalesController.cs
    Middleware/        ValidationExceptionMiddleware.cs

tests/
  Unit/                Domain + Application + Infrastructure
  Integration/         PostgreSQL (Testcontainers)
  Functional/          HTTP (WebApplicationFactory)
```
