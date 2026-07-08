# Implementation Summary

The .NET 10 backend for CartSmart is built and verified end to end. This document summarizes
what was built, how it was verified, and known tradeoffs. For scope rules see `CLAUDE.md`; for
the full business requirements see `Claude_BRD.md`.

## Stack

ASP.NET Core Minimal APIs on .NET 10, EF Core + PostgreSQL, JWT access tokens + rotating
refresh tokens.

## What's implemented

Matches `CLAUDE.md`'s Phase 1 scope exactly:

- **Auth**: email/password, Sign in with Google (`Google.Apis.Auth`), Sign in with Apple (JWKS
  validation against `appleid.apple.com`) — all first-class, all issue the same JWT/refresh
  token pair
- **Device registration** for multi-device sync tracking
- **Shopping list + item sync**: upsert-by-client-id (offline-friendly), soft-delete
  tombstones, delta pull via `GET /sync?since=`
- **Versioned product/category reference list**, seeded from a bundled JSON file
- **GDPR account export** (`GET /account/export`) and **erasure** (`DELETE /account`, cascades
  through everything)
- `schemaVersion` field on sync responses so a Phase 2 suggestion-ranker signal can be added
  additively later, per NFR-6

## What's deliberately absent

No purchase-history table, no prediction-model persistence — nothing in the schema or
endpoints touches that data, per the BRD's core privacy commitment.

## Verification

Installed .NET 10 SDK + PostgreSQL 16 in the sandbox, ran the actual EF Core migration, and
smoke-tested the full flow via curl:

- register → login → device registration → list/item upsert → delta sync showing tombstones
- refresh token rotation (old token correctly rejected after use)
- GDPR delete (confirmed all rows cascade to zero)

## Tooling for future runs

Added `docker-compose.yml` + `.env.example` so Postgres spins up with `docker compose up -d`
anywhere Docker is available, plus a README covering setup, config keys, and the migration
workflow.

## Known tradeoffs

JWT access tokens are stateless, so a deleted/logged-out account's *access* token (not refresh
token) stays cryptographically valid until it naturally expires — 15 minutes by default. This
is a standard tradeoff for stateless JWT, called out here since GDPR erasure is in scope.
