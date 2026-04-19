[Back to README](../README.md)

## Users API

Base path: `/api/v1/users`. All endpoints require `Authorization: Bearer <token>`.

---

### GET /api/v1/users

Retrieves a paginated list of users.

Query parameters: `page` (default: `1`), `size` (default: `10`), `order` (optional, e.g. `username asc`).

Response `200 OK`:

```json
{
  "data": [
    {
      "id": "uuid",
      "email": "string",
      "username": "string",
      "phone": "string",
      "status": "Active | Inactive | Suspended",
      "role": "Customer | Manager | Admin",
      "createdAt": "datetime"
    }
  ],
  "totalItems": 10,
  "currentPage": 1,
  "totalPages": 1,
  "hasNextPage": false,
  "hasPreviousPage": false
}
```

---

### POST /api/v1/users

Creates a new user account. Emits `UserRegisteredEvent`.

Request body:

```json
{
  "email": "string",
  "username": "string",
  "password": "string",
  "phone": "string",
  "status": "Active | Inactive | Suspended",
  "role": "Customer | Manager | Admin"
}
```

Response `201 Created`:

```json
{
  "id": "uuid",
  "email": "string",
  "username": "string",
  "phone": "string",
  "status": "string",
  "role": "string",
  "createdAt": "datetime"
}
```

---

### GET /api/v1/users/{id}

Retrieves a specific user by UUID. Returns `404` if the user does not exist.

Response `200 OK`:

```json
{
  "id": "uuid",
  "email": "string",
  "username": "string",
  "phone": "string",
  "status": "string",
  "role": "string",
  "createdAt": "datetime"
}
```

---

### PUT /api/v1/users/{id}

Updates a user's profile. Returns `404` if the user does not exist.

Request body:

```json
{
  "email": "string",
  "username": "string",
  "phone": "string",
  "status": "Active | Inactive | Suspended",
  "role": "Customer | Manager | Admin"
}
```

Response `200 OK` — same schema as `GET /api/v1/users/{id}`.

---

### DELETE /api/v1/users/{id}

Deletes a user account. Returns `404` if the user does not exist.

Response `200 OK`:

```json
{ "success": true }
```

<br/>
<div style="display: flex; justify-content: space-between;">
  <a href="./sales-api.md">Previous: Sales API</a>
  <a href="./auth-api.md">Next: Auth API</a>
</div>
