# ADR-002: Use REST Snapshot Plus WebSocket Stream for Market Data

## Status

Accepted

## Date

2026-07-03

## Context

InvestView needs a market board that can render an initial table quickly and then receive realtime updates. DNSE exposes both REST endpoints and a WebSocket stream. The local DNSE SDK reference in `docs/openapi-sdk-main` shows that these two integration paths have different authentication flows, payload shapes, and operational concerns.

The frontend must not call DNSE directly because DNSE credentials belong on the backend, and the UI should not depend on DNSE protocol details.

## Decision

Use a two-step market data flow:

1. Load an initial market-board snapshot through InvestView REST APIs backed by `IMarketDataProvider`.
2. Apply realtime quote updates through backend-managed DNSE WebSocket ingestion and app-facing SignalR broadcasts.

The app-facing contracts remain InvestView-owned DTOs. DNSE REST responses and WebSocket messages are normalized inside `InvestView.Infrastructure`.

For MVP, use board `G1` first, use WebSocket `encoding=json`, and subscribe only to channels needed by the market board: trades, top price, security definition, and optionally session state.

## Alternatives Considered

### Frontend connects directly to DNSE WebSocket

- Pros: fewer backend components at first.
- Cons: exposes provider protocol details and risks exposing credentials; makes mock fallback harder; prevents backend normalization and monitoring.
- Rejected because provider connectivity is a backend responsibility.

### REST polling only

- Pros: simpler than WebSocket; easier to test.
- Cons: does not demonstrate realtime handling and can create unnecessary provider load.
- Rejected because realtime market updates are a core project goal.

### WebSocket only

- Pros: one market-data path after connection.
- Cons: slower first render, harder refresh/recovery, requires replay/state reconstruction.
- Rejected because a REST snapshot gives a stable initial state before live updates arrive.

## Consequences

- Backend needs explicit REST and WebSocket adapters.
- WebSocket auth/signature logic, ping/pong handling, reconnect, re-auth, and re-subscribe behavior must be tested in isolation.
- SignalR remains the only realtime contract visible to React.
- Market data DTOs must normalize REST/WebSocket field differences such as `quantity` versus `qtty`.
- Mock snapshot and mock stream can use the same app-facing contracts, so the demo still works without DNSE credentials.
