# CartSmart_API
AI shopping list - cartSmart

Backend API for the CartSmart mobile app (see `Claude_BRD.md`). Phase 1 scope only — see
`CLAUDE.md` for what this backend does and deliberately does not do (no purchase history or
prediction model data is ever stored server-side).

## Stack

- .NET 10 / ASP.NET Core Minimal APIs
- EF Core 10 + PostgreSQL (Npgsql)
- JWT bearer access tokens + rotating opaque refresh tokens
- Email/password, Sign in with Apple, Sign in with Google — first-class, equal auth options

## Project layout

```
src/CartSmart.Api/
  Auth/         JWT issuance, password hashing, Google/Apple id_token validation
  Contracts/    Request/response DTOs
  Data/         DbContext, EF Core migrations, reference-data seed + seeder
  Endpoints/    Minimal API route groups (auth, devices, lists, sync, reference, account)
  Models/       EF Core entities
```

## Running locally

1. Start Postgres:
   ```
   cp .env.example .env
   docker compose up -d
   ```
2. Set a real JWT signing key and OAuth client IDs (don't ship the dev defaults in
   `appsettings.Development.json` anywhere near production):
   ```
   export ConnectionStrings__CartSmartDb="Host=localhost;Port=5432;Database=cartsmart;Username=cartsmart;Password=cartsmart"
   ```
3. Run the API (migrations and reference-data seeding happen automatically on startup):
   ```
   cd src/CartSmart.Api
   dotnet run
   ```

## Configuration

| Key | Purpose |
|---|---|
| `ConnectionStrings:CartSmartDb` | Postgres connection string |
| `Jwt:Issuer` / `Jwt:Audience` / `Jwt:SigningKey` | Access token signing — set `SigningKey` from a secret manager in any real deployment |
| `Jwt:AccessTokenLifetimeMinutes` / `Jwt:RefreshTokenLifetimeDays` | Token lifetimes |
| `Auth:Google:AllowedAudiences` | Google OAuth client ID(s) the mobile app uses |
| `Auth:Apple:AllowedAudiences` | Apple app bundle ID(s) |

## API surface (Phase 1)

- `POST /api/v1/auth/{register,login,google,apple,refresh,logout}`
- `POST /api/v1/auth/password/forgot` — request a reset email (always 200, no account
  enumeration); `POST /api/v1/auth/password/reset` — complete a reset with the emailed token;
  `POST /api/v1/auth/password/change` — authenticated, proactive password change. All three
  only apply to email/password accounts (see `AccountResponse.HasPassword`)
- `GET/POST/DELETE /api/v1/devices` — device registration for multi-device sync
- `PUT/DELETE /api/v1/lists/{listId}` and `/api/v1/lists/{listId}/items/{itemId}` — upsert by
  client-generated id, so writes work the same online or replayed after being offline
- `GET /api/v1/sync?since={timestamp}` — delta pull of changed/deleted lists and items;
  `serverTime` in the response is the cursor to pass back as `since` on the next call
- `GET /api/v1/reference/{version,products}` — versioned bundled product/category list
- `GET/DELETE /api/v1/account`, `GET /api/v1/account/export` — profile data + GDPR export/erasure

## Error responses

Every non-2xx response is a single JSON envelope: `{ "code": "STRING_CODE", "message": "human-readable" }`.
Status codes: `400` validation, `401` bad/expired credentials, `404` not found, `409` optimistic-
concurrency conflict (list/item modified by another device), `422` semantic/business-rule
violation (e.g. duplicate email on register). See `ApiError` in `Contracts/ErrorContracts.cs`
and `Endpoints/ApiResults.cs`.

## Adding a migration

```
cd src/CartSmart.Api
dotnet ef migrations add <Name> -o Data/Migrations
```

## Deliberately not built here (Phase 2+)

Purchase history, prediction model state/weights, cold-start priors, server-side batch
analysis, and federated learning aggregation are all out of scope per `CLAUDE.md` — the
`schemaVersion` field on `SyncResponse` and the delta-sync design exist so a server-provided
suggestion signal could be added later without a breaking change, not because any of that is
implemented now.
