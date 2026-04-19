[Back to README](../README.md)

## General API Definitions

All endpoints are served under `/api/v1/` and require a valid JWT Bearer token unless stated otherwise.

### Authentication Header

```
Authorization: Bearer <token>
```

Obtain a token via `POST /api/v1/auth/login`.

---

### Pagination

Pagination is supported on all list endpoints using the following query parameters:

- `page` — Page number (default: `1`)
- `size` — Number of items per page (default: `10`)

Example:

```
GET /api/v1/sales?page=2&size=20
```

All paginated responses include full navigation metadata:

```json
{
  "data": [],
  "totalItems": 45,
  "currentPage": 2,
  "totalPages": 5,
  "hasNextPage": true,
  "hasPreviousPage": true
}
```

---

### Ordering

Use the `order` query parameter to sort results. Specify one or more fields with optional direction (`asc` / `desc`). Multiple fields are comma-separated.

```
GET /api/v1/sales?order=saleDate desc
GET /api/v1/sales?order=customerName asc,saleDate desc
```

Supported order fields for Sales: `saleNumber`, `saleDate`, `totalAmount`, `customerName`.

Unknown field names fall back to the default order (`createdAt desc`).

---

### Filtering

The Sales list endpoint supports the following filter query parameters:

- `customerName` — Case-insensitive partial match on customer name
- `dateFrom` — Sales on or after this date (ISO 8601, UTC)
- `dateTo` — Sales on or before this date (ISO 8601, UTC)
- `isCancelled` — `true` / `false` to filter by cancellation state

Example:

```
GET /api/v1/sales?customerName=acme&dateFrom=2025-01-01&isCancelled=false
```

---

### Idempotency

The `POST /api/v1/sales` endpoint supports an optional `idempotencyKey` field (UUID) in the request body.

When provided:

- If a sale already exists with that key, the original sale is returned without creating a duplicate.
- If no sale exists with that key, a new sale is created and the key is stored.

This guarantees **at-most-once creation** when clients retry on network timeouts.

```json
{
  "idempotencyKey": "550e8400-e29b-41d4-a716-446655440000",
  "customerId": "...",
  "...": "..."
}
```

---

### Optimistic Concurrency

`PUT /api/v1/sales/{id}` requires a `rowVersion` field in the request body. The value is the `rowVersion` returned by the most recent `GET` for that sale.

If another request has modified the sale between your `GET` and `PUT`, the server returns **HTTP 409 Conflict**. Reload the resource and retry with the updated `rowVersion`.

---

### Error Handling

The API uses standard HTTP response codes:

- `2xx` — Success
- `4xx` — Client error (invalid input, not found, conflict)
- `5xx` — Server error

#### Error Response Format

Every error response body follows this structure:

```json
{
  "type": "string",
  "error": "string",
  "detail": "string",
  "traceId": "string"
}
```

- `type` — Machine-readable error type identifier
- `error` — Short human-readable summary
- `detail` — Specific explanation for this occurrence
- `traceId` — Correlation ID for log tracing (matches the ASP.NET Core `TraceIdentifier`)

#### Error Type Reference

| HTTP Status | `type` | Trigger |
| --- | --- | --- |
| 400 | `ValidationError` | FluentValidation failure |
| 400 | `BusinessRuleViolation` | Domain rule violated (e.g. >20 items) |
| 400 | `InvalidOperation` | Invalid state transition |
| 401 | `Unauthorized` | Missing or invalid JWT |
| 404 | `ResourceNotFound` | Entity not found |
| 409 | `ConcurrencyConflict` | Stale `rowVersion` on PUT |
| 500 | `InternalError` | Unhandled server exception |

#### Example Error Responses

##### Validation Error (400)

```json
{
  "type": "ValidationError",
  "error": "One or more validation errors occurred.",
  "detail": "Items: A sale must have at least one item.",
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01"
}
```

##### Concurrency Conflict (409)

```json
{
  "type": "ConcurrencyConflict",
  "error": "Sale was modified by a concurrent request. Reload and retry.",
  "detail": "Sale was modified by a concurrent request. Reload and retry.",
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01"
}
```

##### Not Found (404)

```json
{
  "type": "ResourceNotFound",
  "error": "Sale with ID 123 was not found.",
  "detail": "Sale with ID 123 was not found.",
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01"
}
```

<br>
<div style="display: flex; justify-content: space-between;">
  <a href="./frameworks.md">Previous: Frameworks</a>
  <a href="./sales-api.md">Next: Sales API</a>
</div>
