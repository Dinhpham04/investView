# Implementation Plan: InvestView MVP

## Overview

Build InvestView as a focused fullstack securities portfolio project for the FSS application. The plan avoids broad upfront build-out. Each milestone leaves the app runnable and proves one important capability from the spec.

Primary strategy:

- Start with a walking skeleton from React to ASP.NET Core.
- Define internal contracts before provider implementations.
- Build market data first because it drives UI, caching, realtime, and later trading.
- Add persistence and simulated trading only after the market board works end-to-end.
- Integrate DNSE after mock and contract boundaries are proven.

## Architecture Decisions

- Use a modular monolith backend: `Api`, `Application`, `Domain`, `Infrastructure`.
- Use React feature folders for frontend workflows.
- Depend on internal contracts, not DNSE payloads. See `docs/decisions/ADR-001-depend-on-internal-contracts.md`.
- Use REST snapshot plus WebSocket stream for market data. See `docs/decisions/ADR-002-market-data-snapshot-plus-stream.md`.
- Use mock market data first, then DNSE behind the same interfaces.
- Use `IMemoryCache` for MVP caching; Redis is out of scope unless approved later.
- Use SignalR for app-facing realtime updates.

## DNSE Source Alignment

Use `docs/openapi-sdk-main` as the implementation reference for DNSE details that are missing or ambiguous in the public docs.

Important SDK-derived constraints:

- REST adapter defaults: base URL `https://openapi.dnse.com.vn`, API version `2026-05-07`.
- REST signing is isolated from business logic and must be unit-tested. The SDK signs method plus path, date header, and nonce; query parameters are sent on the URL but are not part of the SDK signature string.
- REST date header defaults to `Date`, but the header name must be configurable because DNSE documentation/examples may use `X-Aux-Date`.
- WebSocket auth is a separate flow from REST auth: after connect/welcome, sign `api_key:timestamp:nonce` with HMAC-SHA256 hex.
- WebSocket adapter must handle subscribe acknowledgements, `ping`/`pong`, `error`, reconnect, re-auth, re-subscribe, and stream health.
- WebSocket event type mapping must handle at least `t`, `q`, `sd`, and optionally `s` for MVP.
- Internal DTO mapping must normalize REST/WebSocket field differences such as `quantity` versus `qtty`.
- Do not copy unsafe SDK behavior such as disabling TLS certificate validation.

## Dependency Graph

```text
Repository setup and project scaffold
        |
        v
Internal contracts and mock market data
        |
        v
Market board REST vertical slice
        |
        v
Caching decorator and tests
        |
        v
SignalR quote stream with mock data
        |
        v
SQL Server persistence and demo auth
        |
        v
Watchlist, portfolio, simulated orders
        |
        v
DNSE REST/WebSocket adapters
        |
        v
Dockerized demo and interview polish
```

## Milestone 1: Scaffold and Walking Skeleton

Goal: create a runnable app from frontend to backend before adding real business complexity.

### Task 1: Scaffold Backend Solution

**Status:** Done

**Description:** Create the .NET solution, backend projects, test projects, references, basic configuration, Swagger, health endpoint, and build/test pipeline.

**Acceptance criteria:**

- [ ] Solution contains `InvestView.Api`, `InvestView.Application`, `InvestView.Domain`, `InvestView.Infrastructure`.
- [ ] Test projects exist for API, Application, and Domain.
- [ ] API exposes a health endpoint and Swagger in development.
- [ ] Project references enforce the intended dependency direction.

**Verification:**

- [ ] `dotnet restore`
- [ ] `dotnet build`
- [ ] `dotnet test`
- [ ] Manual check: API starts and Swagger opens.

**Dependencies:** None

**Files likely touched:**

- `InvestView.sln`
- `src/InvestView.Api/*`
- `src/InvestView.Application/*`
- `src/InvestView.Domain/*`
- `src/InvestView.Infrastructure/*`
- `tests/InvestView.*.Tests/*`

**Estimated scope:** Medium

### Task 2: Scaffold React App

**Status:** Done

**Description:** Create the React TypeScript app with routing, feature folder structure, API client shell, and a basic shell page that can call the backend health endpoint.

**Acceptance criteria:**

- [ ] React app runs locally.
- [ ] Feature-based folder structure exists.
- [ ] API client wrapper is separate from UI components.
- [ ] The shell page can show backend health status.

**Verification:**

- [ ] `npm install`
- [ ] `npm run build`
- [ ] `npm run test`
- [ ] Manual check: frontend starts and calls backend health endpoint.

**Dependencies:** Task 1

**Files likely touched:**

- `src/investview.web/*`

**Estimated scope:** Medium

### Checkpoint: Walking Skeleton

**Status:** Done

- [x] Backend builds and tests pass.
- [x] Frontend builds and tests pass.
- [x] Frontend can call backend.
- [x] No business logic has been added prematurely.

## Milestone 2: Market Data Contracts, Mock Provider, and Caching

Goal: establish the market data boundary before DNSE integration.

### Task 3: Add Market Data Contracts and Mock Provider

**Status:** Done

**Description:** Define app-owned market data DTOs and interfaces, then implement mock quote data behind `IMarketDataProvider`. Contracts should be shaped for the future DNSE REST snapshot plus WebSocket stream design, without depending on DNSE payload types.

**Acceptance criteria:**

- [x] `MarketQuoteDto`, `SymbolDetailDto`, and `OhlcBarDto` exist in Application.
- [x] `PriceLevelDto` or equivalent exists for bid/ask levels.
- [x] Quote DTOs include the fields needed by market board snapshot and updates: symbol, board, last price, changed value/percent, volume, value, reference/ceiling/floor, bid/ask levels, trading status, and updated time.
- [x] `IMarketDataProvider` exists in Application abstractions.
- [x] `IMarketDataStream` or a planned stream update DTO exists if needed to keep snapshot and realtime contracts aligned.
- [x] `MockMarketDataProvider` lives in Infrastructure.
- [x] API returns mock market board data without exposing infrastructure types.
- [x] Contract names are provider-neutral and do not contain `Dnse`.

**Verification:**

- [x] Unit tests prove mock provider returns stable sample quotes.
- [x] API test proves `GET /api/market/quotes` returns expected DTO shape.
- [x] Tests prove bid/ask level structure and timestamp fields serialize predictably.
- [x] `dotnet build`
- [x] `dotnet test`

**Dependencies:** Task 1

**Files likely touched:**

- `src/InvestView.Application/Abstractions/*`
- `src/InvestView.Application/Dtos/*`
- `src/InvestView.Infrastructure/MarketData/*`
- `src/InvestView.Api/Controllers/*`
- `tests/InvestView.Api.Tests/*`

**Estimated scope:** Medium

### Task 4: Add Cached Market Data Provider

**Status:** Done

**Description:** Add `CachedMarketDataProvider` as a decorator around `IMarketDataProvider` using `IMemoryCache`.

**Acceptance criteria:**

- [x] Market board reads go through cache.
- [x] TTL values are explicit and configurable.
- [x] Development logs can show cache hit/miss.
- [x] Existing provider contract does not change.

**Verification:**

- [x] Unit tests cover cache hit and cache miss behavior.
- [x] `dotnet build`
- [x] `dotnet test`

**Dependencies:** Task 3

**Files likely touched:**

- `src/InvestView.Infrastructure/MarketData/*`
- `src/InvestView.Api/Program.cs`
- `tests/InvestView.Application.Tests/*` or `tests/InvestView.Api.Tests/*`

**Estimated scope:** Small

### Task 5: Build Market Board UI

**Description:** Build the first real UI slice: React market board consumes `GET /api/market/quotes` and renders dense securities data.

**Acceptance criteria:**

- [ ] Market board page renders symbol, last price, change, percent change, volume, and trading status.
- [ ] API client types are separate from components.
- [ ] UI handles loading and error states.
- [ ] Layout works on desktop and mobile widths.

**Verification:**

- [ ] Component tests cover market board render states.
- [ ] `npm run build`
- [ ] `npm run test`
- [ ] Manual browser check against backend.

**Dependencies:** Task 3

**Files likely touched:**

- `src/investview.web/src/features/market-board/*`
- `src/investview.web/src/shared/api/*`
- `src/investview.web/src/app/*`

**Estimated scope:** Medium

### Checkpoint: Market Board REST

- [ ] Market board works end-to-end with mock data.
- [ ] Backend contracts are provider-neutral.
- [ ] Caching behavior is tested.
- [ ] Frontend does not depend on DNSE payloads.

## Milestone 3: Realtime Quote Updates

Goal: prove realtime data handling before adding trading complexity.

### Task 6: Add SignalR Quote Hub with Mock Stream

**Description:** Add backend SignalR `QuoteHub` and a mock quote update service that broadcasts quote changes. This establishes the app-facing realtime contract before DNSE WebSocket is introduced.

**Acceptance criteria:**

- [ ] API exposes `/hubs/quotes`.
- [ ] Mock stream publishes quote updates for known symbols.
- [ ] SignalR payload uses internal `MarketQuoteDto` or a dedicated app-owned update DTO.
- [ ] SignalR contract supports partial quote updates without exposing DNSE message type names.
- [ ] Stream can be disabled in tests/config.

**Verification:**

- [ ] Backend tests cover hub/service behavior where practical.
- [ ] `dotnet build`
- [ ] `dotnet test`
- [ ] Manual check with a simple client or frontend connection.

**Dependencies:** Task 3

**Files likely touched:**

- `src/InvestView.Api/Hubs/*`
- `src/InvestView.Infrastructure/Realtime/*`
- `src/InvestView.Api/Program.cs`
- `tests/InvestView.Api.Tests/*`

**Estimated scope:** Medium

### Task 7: Connect Frontend to Realtime Quotes

**Description:** Add frontend SignalR client and update the market board from live quote messages.

**Acceptance criteria:**

- [ ] SignalR connection is isolated in `shared/realtime`.
- [ ] Market board updates changed rows without replacing unrelated UI state.
- [ ] Connection state is visible enough for debugging.
- [ ] UI still works if realtime connection fails.

**Verification:**

- [ ] Component or hook tests cover quote update handling.
- [ ] `npm run build`
- [ ] `npm run test`
- [ ] Manual browser check: quote rows update.

**Dependencies:** Task 6

**Files likely touched:**

- `src/investview.web/src/shared/realtime/*`
- `src/investview.web/src/features/market-board/*`

**Estimated scope:** Medium

### Checkpoint: Realtime Market Board

- [ ] REST initial load works.
- [ ] SignalR updates work.
- [ ] App remains usable when realtime is unavailable.
- [ ] Realtime DTOs are internal app contracts.

## Milestone 4: Persistence, Watchlist, Portfolio, and Simulated Orders

Goal: add backend domain depth and the main investor workflow.

### Task 8: Add Persistence and Demo Auth Foundation

**Description:** Add EF Core SQL Server setup, initial entities, migrations, seed data, and demo JWT login.

**Acceptance criteria:**

- [ ] SQL Server provider is configured.
- [ ] Initial schema supports users, watchlists, cash accounts, holdings, orders, and executions.
- [ ] Demo user can log in and receive JWT.
- [ ] Secrets and connection strings are environment-based.

**Verification:**

- [ ] EF mapping tests or integration tests cover important relationships.
- [ ] API test covers demo login.
- [ ] `dotnet build`
- [ ] `dotnet test`

**Dependencies:** Task 1

**Files likely touched:**

- `src/InvestView.Domain/*`
- `src/InvestView.Infrastructure/Data/*`
- `src/InvestView.Api/Controllers/AuthController.cs`
- `src/InvestView.Api/Program.cs`
- `tests/InvestView.Api.Tests/*`

**Estimated scope:** Medium

### Task 9: Add Watchlist Flow

**Description:** Implement watchlist REST endpoints and frontend watchlist interactions using authenticated demo user state.

**Acceptance criteria:**

- [ ] User can list watchlist symbols.
- [ ] User can add a valid symbol.
- [ ] User can remove a symbol.
- [ ] Duplicate watchlist items are handled predictably.
- [ ] Frontend can add/remove from market board or watchlist panel.

**Verification:**

- [ ] API tests cover add/remove/list.
- [ ] Frontend tests cover watchlist interactions.
- [ ] `dotnet test`
- [ ] `npm run test`
- [ ] Manual browser check.

**Dependencies:** Task 5, Task 8

**Files likely touched:**

- `src/InvestView.Application/Watchlists/*`
- `src/InvestView.Api/Controllers/WatchlistController.cs`
- `src/InvestView.Infrastructure/Data/*`
- `src/investview.web/src/features/watchlist/*`
- `src/investview.web/src/features/market-board/*`

**Estimated scope:** Medium

### Task 10: Add Simulated Order and Portfolio Flow

**Description:** Implement order placement/cancellation, cash and holding updates, portfolio summary, and frontend order ticket.

**Acceptance criteria:**

- [ ] Buy orders require sufficient simulated cash.
- [ ] Sell orders require sufficient available holdings.
- [ ] Filled orders update cash and holdings in one transaction.
- [ ] User can see portfolio summary and order history.
- [ ] UI clearly labels all trading as simulated.

**Verification:**

- [ ] Domain tests cover cash, holdings, order status transitions, and execution creation.
- [ ] API tests cover place order, cancel order, and portfolio query.
- [ ] Frontend tests cover order ticket validation and portfolio display.
- [ ] `dotnet test`
- [ ] `npm run test`
- [ ] Manual demo: place order and see portfolio update.

**Dependencies:** Task 8, Task 9

**Files likely touched:**

- `src/InvestView.Domain/Orders/*`
- `src/InvestView.Domain/Holdings/*`
- `src/InvestView.Domain/CashAccounts/*`
- `src/InvestView.Application/Orders/*`
- `src/InvestView.Application/Portfolio/*`
- `src/InvestView.Api/Controllers/OrdersController.cs`
- `src/InvestView.Api/Controllers/PortfolioController.cs`
- `src/investview.web/src/features/order-ticket/*`
- `src/investview.web/src/features/portfolio/*`

**Estimated scope:** Medium

### Checkpoint: Core Investor Workflow

- [ ] Login works.
- [ ] Market board works.
- [ ] Watchlist works.
- [ ] Simulated order flow works.
- [ ] Portfolio updates after filled simulated orders.
- [ ] Core domain rules are covered by tests.

## Milestone 5: DNSE Integration and Demo Packaging

Goal: add real provider integration behind stable contracts and package the demo.

### Task 11: Add DNSE REST Adapter

**Description:** Implement `DnseMarketDataProvider` behind `IMarketDataProvider` and map DNSE REST responses into internal DTOs. Use the local SDK and DNSE docs as the source of truth for endpoints, signature shape, API version, and header names.

**Acceptance criteria:**

- [ ] DNSE credentials/config are read from environment/configuration.
- [ ] Missing credentials fall back to mock provider or fail with a clear startup/config message.
- [ ] DNSE response models do not leave Infrastructure.
- [ ] REST adapter supports market board, symbol detail, and OHLC data needed by MVP.
- [ ] REST client uses default base URL `https://openapi.dnse.com.vn` unless overridden.
- [ ] REST client sends API version `2026-05-07` unless overridden.
- [ ] REST date header name is configurable and defaults to `Date`.
- [ ] REST signer is isolated and signs method, path, date header, and nonce as shown by the local SDK.
- [ ] Query parameters are handled consistently with the SDK: present in the request URL but not in the signed path string.
- [ ] Adapter uses TLS certificate validation; it does not copy the SDK's disabled-cert behavior.

**Verification:**

- [ ] Unit tests cover DNSE response mapping using fixture JSON.
- [ ] Unit tests cover REST signature construction with deterministic timestamp and nonce.
- [ ] Unit tests cover URL/query construction for `/instruments`, `/price/{symbol}/secdef`, `/price/{symbol}/trades/latest`, `/price/{symbol}/quotes/latest`, and `/price/ohlc`.
- [ ] Integration path can be manually verified when credentials are available.
- [ ] `dotnet build`
- [ ] `dotnet test`

**Dependencies:** Task 4

**Files likely touched:**

- `src/InvestView.Infrastructure/Dnse/*`
- `src/InvestView.Infrastructure/MarketData/*`
- `src/InvestView.Api/Program.cs`
- `tests/InvestView.Application.Tests/*` or `tests/InvestView.Api.Tests/*`

**Estimated scope:** Medium

### Task 12: Add DNSE WebSocket Adapter

**Description:** Implement DNSE inbound WebSocket client and forward normalized quote updates through existing SignalR flow. Use the local SDK WebSocket implementation as the reference for auth, channel names, message types, heartbeat, and reconnect behavior.

**Acceptance criteria:**

- [ ] DNSE WebSocket auth/signature logic is isolated in Infrastructure.
- [ ] Client can subscribe, handle ping/pong, reconnect, and resubscribe.
- [ ] DNSE messages are mapped to internal quote update DTOs.
- [ ] Mock realtime remains available without DNSE credentials.
- [ ] WebSocket auth signs `api_key:timestamp:nonce` with HMAC-SHA256 hex and is not confused with REST HTTP signing.
- [ ] MVP subscribes to `tick.G1.json`, `top_price.G1.json`, and `security_definition.G1.json`.
- [ ] Optional session state uses `session.{productGroupId}.G1.json` only when needed by the UI.
- [ ] Adapter handles control `action` values and market data type `T` values.
- [ ] Adapter tracks stream health: connected, authenticated, last pong, last message time, and active subscriptions.
- [ ] Per-symbol update ordering is preserved before broadcasting updates through SignalR.

**Verification:**

- [ ] Unit tests cover signature construction and message mapping.
- [ ] Unit tests cover ping/pong handling, reconnect re-subscription state, and unknown message tolerance.
- [ ] Fixture tests cover at least trade `t`, quote `q`, security definition `sd`, and session `s` if session is implemented.
- [ ] Manual verification with credentials if available.
- [ ] `dotnet build`
- [ ] `dotnet test`
- [ ] Manual browser check: frontend receives updates through SignalR.

**Dependencies:** Task 6, Task 11

**Files likely touched:**

- `src/InvestView.Infrastructure/Dnse/*`
- `src/InvestView.Infrastructure/Realtime/*`
- `src/InvestView.Api/Program.cs`
- `tests/InvestView.Application.Tests/*` or `tests/InvestView.Api.Tests/*`

**Estimated scope:** Medium

### Task 13: Add Docker Compose and Demo Documentation

**Description:** Package the MVP for local demo and document how to run and explain it.

**Acceptance criteria:**

- [ ] Docker Compose starts API, web, and SQL Server.
- [ ] `.env.example` documents required local settings without secrets.
- [ ] README explains quick start, architecture, commands, and demo flow.
- [ ] Demo can run with mock data without DNSE credentials.

**Verification:**

- [ ] `docker compose up --build`
- [ ] `dotnet test`
- [ ] `npm run test`
- [ ] Manual full demo flow from login to simulated order.

**Dependencies:** Task 10

**Files likely touched:**

- `docker-compose.yml`
- `.env.example`
- `README.md`
- `src/InvestView.Api/Dockerfile`
- `src/investview.web/Dockerfile`

**Estimated scope:** Medium

### Checkpoint: MVP Ready

- [ ] Full manual demo passes.
- [ ] Automated backend and frontend tests pass.
- [ ] Docker Compose demo works with mock data.
- [ ] DNSE integration can be enabled by config when credentials exist.
- [ ] README gives a clear interview story.

## Risks and Mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| DNSE credentials are unavailable | Medium | Build mock provider and mock stream first; DNSE adapters are late milestone. |
| DNSE payload shape changes | Medium | Keep DNSE models in Infrastructure and map to internal DTOs. |
| Foundation becomes too large | High | Milestone 1 only creates walking skeleton, not full domain/database. |
| Realtime adds complexity before UI is stable | Medium | Build REST market board first, then add SignalR. |
| Trading domain causes broad rewrites | High | Add order/portfolio only after market contracts and persistence are stable. |
| Docker slows early development | Medium | Add Docker Compose after core workflow, except minimal config if needed for SQL Server. |

## Open Questions

1. Which frontend styling approach should be used for MVP: Tailwind CSS, CSS Modules, MUI, Ant Design, or minimal custom CSS?
2. Will DNSE credentials be available during implementation, or should DNSE tasks stay behind fixture tests until later?
3. Should demo auth be seeded-login only, or should simple registration be included?
4. Should the initial market board focus only on Vietnamese symbols from DNSE?

## Plan Control

- Do not start a later milestone if the current checkpoint fails.
- Do not introduce Redis, Kubernetes, real order submission, or admin workflows without updating `SPEC.md`.
- Do not change app-owned market DTOs just to match DNSE payloads. Map DNSE into the app contracts instead.
- Keep each implementation commit tied to one task.
