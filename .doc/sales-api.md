[Back to README](../README.md)

## Sales API

Base path: `/api/v1/sales`. All endpoints require `Authorization: Bearer <token>`.

---

### POST /api/v1/sales

Creates a new sale. Accepts an optional `idempotencyKey` (UUID) to prevent duplicate creation on retries — if a sale with that key already exists it is returned as-is without inserting a new row.

Discounts are calculated server-side by quantity per product:

- 1–3 items: no discount
- 4–9 items: 10% discount
- 10–20 items: 20% discount
- More than 20 items: rejected with `400 Business Rule Violation`

Request body:

```json
{
  "idempotencyKey": "550e8400-e29b-41d4-a716-446655440000",
  "customerId": "uuid",
  "customerName": "string (max 150)",
  "branchId": "uuid",
  "branchName": "string (max 150)",
  "saleDate": "2025-06-01T00:00:00Z",
  "items": [
    {
      "productId": "uuid",
      "productName": "string (max 200)",
      "quantity": 4,
      "unitPrice": 50.00
    }
  ]
}
```

Response `201 Created`:

```json
{
  "id": "uuid",
  "saleNumber": "20250601-A1B2C3D4",
  "saleDate": "2025-06-01T00:00:00Z",
  "customerId": "uuid",
  "customerName": "string",
  "branchId": "uuid",
  "branchName": "string",
  "totalAmount": 180.00,
  "isCancelled": false,
  "rowVersion": 0,
  "items": [
    {
      "id": "uuid",
      "productId": "uuid",
      "productName": "string",
      "quantity": 4,
      "unitPrice": 50.00,
      "discount": 0.10,
      "totalAmount": 180.00,
      "isCancelled": false
    }
  ]
}
```

---

### GET /api/v1/sales

Returns a paginated, filterable list of sales.

Query parameters:

- `page` — Page number (default: `1`)
- `size` — Items per page (default: `10`)
- `order` — Sort expression, e.g. `saleDate desc` or `customerName asc,totalAmount desc`. Supported fields: `saleNumber`, `saleDate`, `totalAmount`, `customerName`.
- `customerName` — Case-insensitive partial match
- `dateFrom` — Sales on or after this date (ISO 8601 UTC)
- `dateTo` — Sales on or before this date (ISO 8601 UTC)
- `isCancelled` — `true` or `false`

Response `200 OK`:

```json
{
  "data": [
    {
      "id": "uuid",
      "saleNumber": "string",
      "saleDate": "datetime",
      "customerId": "uuid",
      "customerName": "string",
      "branchId": "uuid",
      "branchName": "string",
      "totalAmount": 0.00,
      "isCancelled": false,
      "itemCount": 3
    }
  ],
  "totalItems": 45,
  "currentPage": 1,
  "totalPages": 5,
  "hasNextPage": true,
  "hasPreviousPage": false
}
```

---

### GET /api/v1/sales/{id}

Returns a single sale with all its line items. Returns `404` if the sale does not exist.

Response `200 OK`:

```json
{
  "id": "uuid",
  "saleNumber": "string",
  "saleDate": "datetime",
  "customerId": "uuid",
  "customerName": "string",
  "branchId": "uuid",
  "branchName": "string",
  "totalAmount": 200.00,
  "isCancelled": false,
  "rowVersion": 2,
  "items": [
    {
      "id": "uuid",
      "productId": "uuid",
      "productName": "string",
      "quantity": 10,
      "unitPrice": 25.00,
      "discount": 0.20,
      "totalAmount": 200.00,
      "isCancelled": false
    }
  ]
}
```

---

### PUT /api/v1/sales/{id}

Fully replaces a sale's header and all its items in a single atomic operation. Requires the `rowVersion` last returned by a `GET` for optimistic concurrency. Returns `409 Conflict` if the sale was modified by another request since the last read.

Request body:

```json
{
  "customerId": "uuid",
  "customerName": "string",
  "branchId": "uuid",
  "branchName": "string",
  "saleDate": "datetime",
  "rowVersion": 2,
  "items": [
    {
      "productId": "uuid",
      "productName": "string",
      "quantity": 5,
      "unitPrice": 30.00
    }
  ]
}
```

Response `200 OK` — same schema as `GET /api/v1/sales/{id}`, with `rowVersion` incremented by 1.

Errors: `400` validation/business rule, `404` not found, `409` stale `rowVersion`.

---

### DELETE /api/v1/sales/{id}

Soft-cancels a sale. Sale records are never physically deleted (they are immutable financial history). This operation is **idempotent** — calling it on an already-cancelled sale returns `200 OK` without raising a second event.

Response `200 OK`:

```json
{ "success": true }
```

Error: `404` if the sale does not exist.

---

### PATCH /api/v1/sales/{id}/items/{itemId}/cancel

Cancels a single line item within a sale. The cancelled item is excluded from `totalAmount`. The sale itself remains active.

Response `200 OK`:

```json
{
  "saleId": "uuid",
  "itemId": "uuid",
  "isCancelled": true,
  "newSaleTotalAmount": 150.00
}
```

Errors: `400` if item or sale is already cancelled, `404` if not found.

---

### Domain Events

Every mutation publishes domain events via the Transactional Outbox. Events are delivered at-least-once and logged as structured JSON by default.

- `SaleCreatedEvent` — `POST /sales`. Contains a full item snapshot.
- `SaleModifiedEvent` — `PUT /sales/{id}`. Contains previous and new header values plus `previousTotalAmount` / `newTotalAmount`.
- `SaleCancelledEvent` — `DELETE /sales/{id}` (first cancellation only).
- `ItemCancelledEvent` — Emitted for each active item removed during `PUT` or on `PATCH /items/{itemId}/cancel`.
- `ItemAddedEvent` — Emitted for each item added during `PUT /sales/{id}`.

All events implement `IDomainEvent` and carry `occurredAt` (UTC timestamp) and `version` (currently `1`). The `version` field allows the Outbox processor to route messages to a compatible deserializer when a breaking schema change is introduced, without reprocessing or silently discarding events already in the queue.

[Previous: General API](./general-api.md) | [Next: Users API](./users-api.md)
