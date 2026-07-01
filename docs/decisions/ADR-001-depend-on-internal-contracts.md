# ADR-001: Depend on Internal Contracts, Not External Provider Payloads

## Status

Accepted

## Date

2026-07-01

## Context

InvestView integrates with DNSE for market data, but the project must remain maintainable and easy to explain in interviews. External APIs and WebSocket payloads can change shape, require authentication details, or expose more data than the app needs.

If controllers, React components, or domain services depend directly on DNSE response models, a provider change would spread through the whole codebase. That creates rework and makes tests harder to write.

## Decision

InvestView code will depend on internal application contracts, not DNSE payloads.

DNSE-specific models and protocol handling belong only in `InvestView.Infrastructure`.

Application, API, domain, and frontend layers must use InvestView-owned DTOs and interfaces, such as:

- `IMarketDataProvider`
- `IMarketDataStream`
- `MarketQuoteDto`
- `SymbolDetailDto`
- `OhlcBarDto`

The backend maps DNSE REST/WebSocket payloads into internal DTOs before data reaches controllers, SignalR hubs, application services, or the React app.

## Alternatives Considered

### Expose DNSE payloads directly to the frontend

- Pros: faster to start, less mapping code.
- Cons: leaks provider details, exposes unstable contracts to UI, makes frontend hard to test, risks credential/protocol leakage.
- Rejected because it creates tight coupling and weakens the architecture story.

### Use DNSE SDK types throughout backend

- Pros: fewer custom types at first.
- Cons: application and domain code become provider-aware, making mock data and future provider changes harder.
- Rejected because provider integration is an infrastructure concern.

### Create internal contracts at the application boundary

- Pros: stable app-owned API, easy mock fallback, easier tests, clearer separation of concerns.
- Cons: requires mapping code and contract maintenance.
- Accepted because this is the right long-term boundary for a fullstack portfolio project.

## Consequences

- DNSE integration can be implemented later without changing React components or application services.
- Mock market data can use the same contracts as DNSE-backed data.
- Tests can target internal contracts without real DNSE credentials.
- Mapping code must be maintained in infrastructure.
- Any new third-party provider must be adapted behind existing contracts unless a spec update approves a contract change.

## Rule of Thumb

If a type name contains `Dnse`, it must not cross out of `InvestView.Infrastructure`.
