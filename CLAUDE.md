# CLAUDE.md — Backend API

Guidance for Claude Code when working on the backend API for the **AI-Powered Predictive Shopping List App** (BRD v1.0, June 28 2026). This file covers **backend/server scope only**. The on-device prediction engine, mobile UI, and client-side ML are out of scope for this codebase.

## Project Context

Phase 1 is built around one core decision: **all purchase-history data and the prediction model live entirely on-device.** The backend is intentionally thin in Phase 1 — it exists to handle accounts, auth, and syncing shopping-list *content* (not predictions). It must, however, be built so Phase 2 (server-assisted predictions) can be added without a redesign.

When making implementation decisions, default to the smallest backend surface that satisfies the requirements below. Do not add prediction, ML, or analytics endpoints unless explicitly asked — that's Phase 2+ and is documented here only for context.

## Backend Scope — Phase 1 (Build This)

- Account creation and authentication: email/password, Sign in with Apple, Sign in with Google
- Multi-device sync of **shopping list data only** (list items: name, quantity, unit, category, checked state)
- Serving/distributing the bundled product/category reference list (used for client-side autocomplete), including versioned updates via app updates
- Standard account/profile data handling (email, auth tokens, device registration for sync)

## Explicitly Out of Scope for the Backend (Phase 1)

Do not build these unless the user asks for Phase 2 work specifically:

- Any endpoint that receives, stores, or processes purchase history (item, date, quantity)
- Any endpoint that receives or stores prediction model state (per-item intervals, weights)
- Server-side aggregate or cross-user prediction models
- Cold-start priors served from the server (Phase 2, Option A)
- Server-side batch analysis / cross-item correlation (Phase 2, Option B)
- Federated learning aggregation of model weight updates (Phase 2, Option C)
- Receipt OCR, grocery delivery integrations, voice assistant integrations, analytics dashboards

**Hard rule:** raw purchase history and item-level usage data must never leave the user's device in Phase 1 (NFR-2). If a task description implies capturing that data server-side, flag it rather than implementing it — it likely conflicts with the BRD's privacy commitment.

## Data Model (Backend-Owned Data Only)

| Entity | Stored Where | Notes |
|---|---|---|
| Shopping list items | Server (synced) | name, quantity, unit, category, checked state |
| User account/profile | Server | email, auth provider, auth tokens |
| Product/category reference list | Server → bundled with app | Versioned; delivered via app update mechanism, not live sync |
| Purchase history | **Never** (device only) | Do not add a table/model for this |
| Prediction model state | **Never** (device only) | Do not add a table/model for this |

## Non-Functional Requirements Relevant to Backend

- **Privacy/Compliance:** align with GDPR/CCPA for account and profile data. Purchase data isn't handled server-side in Phase 1, so this is limited to standard account-data handling (consent, deletion/export of profile data, token security).
- **Offline-first:** only account sign-in and cross-device sync require connectivity; every other client feature must work without the backend. Don't design APIs that the client depends on for core (non-sync) functionality.
- **Extensibility (NFR-6):** design sync/list APIs so a future "suggestion ranker" input (server-provided signals) can be added later without breaking the existing schema. Version the list/sync data schema from the start.
- **Data durability:** synced data must persist reliably; consider this in migration and backup strategy.

## Phase 2 Awareness (Do Not Build, But Don't Block)

Three future server-side paths are documented in the BRD for planning continuity only:

1. **Option A — Server-provided cold-start priors** (low complexity, recommended first Phase 2 step): server would supply anonymized "typical interval per category" data.
2. **Option B — Server-side batch analysis** (medium complexity): aggregated usage signals uploaded periodically, server computes correlations/seasonal adjustments.
3. **Option C — Federated learning** (high complexity): only model weight updates aggregated server-side, never raw data.

When designing schemas or API contracts now, keep these paths viable (e.g., a versioned schema, a pluggable suggestion-ranker interface) but do not implement any of them.

## Conventions

- Treat any request to add purchase-history or prediction-model persistence to the backend as a scope question — confirm it's an intentional Phase 2 decision before implementing.
- Keep sync endpoints scoped to list content; don't fold prediction-related fields into list/sync payloads.
- Auth: support email, Apple, and Google sign-in as first-class, equal options.
