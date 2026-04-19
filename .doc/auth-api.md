[Back to README](../README.md)

## Auth API

Base path: `/api/v1/auth`. Authentication endpoints are public — no Bearer token required.

---

### POST /api/v1/auth/login

Authenticates a user with email and password. Returns a signed JWT Bearer token to be used on all other endpoints.

Request body:

```json
{
  "email": "user@example.com",
  "password": "string"
}
```

Response `200 OK`:

```json
{
  "token": "string",
  "email": "string",
  "name": "string",
  "role": "Customer | Manager | Admin"
}
```

Errors: `400` validation failure (missing fields), `401` invalid credentials.

---

[Previous: Users API](./users-api.md) | [Next: Project Structure](./project-structure.md)
