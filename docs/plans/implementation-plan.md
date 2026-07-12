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
- Use Redis-backed latest market state with local in-memory mirrors for production-grade market data distribution. See `docs/decisions/ADR-004-redis-backed-market-state.md`.
- Use SignalR for app-facing realtime updates.
- Use Tailwind CSS and follow `docs/design.md` for frontend UI/UX rules.
- Use AG Grid Community for the market board UI. See `docs/decisions/ADR-003-use-ag-grid-for-market-board.md`.

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
DNSE REST snapshot adapter
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
- [x] `dotnet build`
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

### Task 4: Add Local REST Cache Provider

**Status:** Superseded by Task 17

**Description:** The earlier implementation added a process-local REST cache decorator around `IMarketDataProvider`.

**Superseded note:** This implementation was removed when the project moved to the production Redis-backed market state path. Runtime market data freshness is now centralized in Redis; local memory is only a hot mirror populated from Redis Pub/Sub/state reads.

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

**Status:** Done

**Description:** Build the first real UI slice: React market board consumes `GET /api/market/quotes` and renders a dense Vietnamese securities-style board with AG Grid Community. This task is REST snapshot only; realtime updates are added in Task 7.

**Acceptance criteria:**

- [x] Tailwind CSS is configured for `src/investview.web`.
- [x] Frontend follows `docs/design.md` for workstation layout, market-board columns, and price color tokens.
- [x] AG Grid Community is added as the market board grid dependency.
- [x] Market board page renders grouped columns for `CK`, `Trần`, `Sàn`, `TC`, `Bên mua`, `Khớp lệnh`, `Bên bán`, total volume, high, low, status, and updated time.
- [x] Bid and ask depth display 3 price/quantity levels from `bidLevels` and `askLevels`.
- [x] Matched quote columns display last price, last quantity, absolute change, percent change, and total volume.
- [x] Securities color rules are implemented for ceiling, floor, reference, increase, decrease, and unchanged values.
- [x] Symbol column is pinned/sticky and the wide board supports horizontal scrolling.
- [x] API client types are separate from components.
- [x] Quote-to-grid mapping and price formatting are testable outside the grid component.
- [x] UI handles loading and error states.
- [x] Layout works on desktop and mobile widths.
- [x] The UI does not imply data is live realtime until Task 7 is complete.

**Verification:**

- [x] Component tests cover market board render states.
- [x] Unit tests cover quote mapping, price formatting, and color classification.
- [x] `npm run lint`
- [x] `npm run build`
- [x] `npm run test`
- [x] Manual browser check against backend.

**Dependencies:** Task 3

**Files likely touched:**

- `src/investview.web/src/features/market-board/*`
- `src/investview.web/src/shared/api/*`
- `src/investview.web/src/app/*`

**Estimated scope:** Medium

### Checkpoint: Market Board REST

- [x] Market board works end-to-end with mock data.
- [x] Backend contracts are provider-neutral.
- [x] Caching behavior is tested.
- [x] Frontend does not depend on DNSE payloads.

## Milestone 3: Real Market Snapshot and Realtime Quote Updates

Goal: connect the REST snapshot path to DNSE before proving realtime data handling.

### Task 6: Add DNSE REST Snapshot Adapter

**Status:** Done

**Description:** Implement `DnseMarketDataProvider` behind `IMarketDataProvider` so the existing market board can load a real DNSE REST snapshot without changing the React API contract. This task integrates snapshot data first; DNSE WebSocket remains a later task.

**Acceptance criteria:**

- [x] DNSE credentials/config are read from environment/configuration.
- [x] Market data provider can be selected by config: `Mock` or `Dnse`.
- [x] Missing DNSE credentials fall back to mock provider or fail with a clear config message.
- [x] DNSE response models do not leave Infrastructure.
- [x] REST adapter supports the market board snapshot using `/instruments`, `/price/{symbol}/secdef`, `/price/{symbol}/trades/latest`, `/price/{symbol}/quotes/latest`, and `/price/{symbol}/foreign-trading` where available.
- [x] REST client uses default base URL `https://openapi.dnse.com.vn` unless overridden.
- [x] REST client sends API version `2026-05-07` unless overridden.
- [x] REST date header name is configurable and defaults to `Date`.
- [x] REST signer is isolated and signs method, path, date header, and nonce as shown by the local SDK.
- [x] Query parameters are present in the request URL but not in the signed path string.
- [x] Adapter uses normal .NET TLS certificate validation; it does not copy the SDK's disabled-cert behavior.

**Verification:**

- [x] Unit tests cover REST signature construction with deterministic timestamp and nonce.
- [x] Unit tests cover URL/query construction for `/instruments`, `/price/{symbol}/secdef`, `/price/{symbol}/trades/latest`, `/price/{symbol}/quotes/latest`, and `/price/{symbol}/foreign-trading`.
- [x] Unit tests cover DNSE response mapping using fixture JSON.
- [ ] Integration path can be manually verified when DNSE credentials are available.
- [x] `dotnet build`
- [x] `dotnet test`

**Dependencies:** Task 4, Task 5

**Files likely touched:**

- `src/InvestView.Infrastructure/Dnse/*`
- `src/InvestView.Infrastructure/MarketData/*`
- `src/InvestView.Infrastructure/DependencyInjection.cs`
- `tests/InvestView.Api.Tests/*`

**Estimated scope:** Medium

### Task 7: Add SignalR Quote Hub with Mock Stream

**Status:** Done

**Description:** Add backend SignalR `QuoteHub` and a mock quote update service that broadcasts quote changes. This establishes the app-facing realtime contract before DNSE WebSocket is introduced.

**Acceptance criteria:**

- [x] API exposes `/hubs/quotes`.
- [x] Mock stream publishes quote updates for known symbols.
- [x] SignalR payload uses internal `MarketQuoteDto` or a dedicated app-owned update DTO.
- [x] SignalR contract supports partial quote updates without exposing DNSE message type names.
- [x] Stream can be disabled in tests/config.

**Verification:**

- [x] Backend tests cover hub/service behavior where practical.
- [x] `dotnet build`
- [x] `dotnet test`
- [ ] Manual check with a simple client or frontend connection.

**Dependencies:** Task 3

**Files likely touched:**

- `src/InvestView.Api/Hubs/*`
- `src/InvestView.Infrastructure/Realtime/*`
- `src/InvestView.Api/Program.cs`
- `tests/InvestView.Api.Tests/*`

**Estimated scope:** Medium

### Task 8: Connect Frontend to Realtime Quotes

**Status:** Done

**Description:** Add frontend SignalR client and update the market board from live quote messages.

**Acceptance criteria:**

- [x] SignalR connection is isolated in `shared/realtime`.
- [x] Market board updates changed rows through stable row identity and AG Grid transactions instead of replacing unrelated UI state.
- [x] Connection state is visible enough for debugging.
- [x] UI still works if realtime connection fails.

**Verification:**

- [x] Component or hook tests cover quote update handling.
- [x] `npm run build`
- [x] `npm run test`
- [x] Manual SignalR smoke test: Node client received quote updates from API mock stream.
- [ ] Manual browser check: quote rows update.

**Dependencies:** Task 7

**Files likely touched:**

- `src/investview.web/src/shared/realtime/*`
- `src/investview.web/src/features/market-board/*`

**Estimated scope:** Medium

### Checkpoint: Realtime Market Board

- [x] REST initial load works.
- [x] SignalR updates work.
- [x] App remains usable when realtime is unavailable.
- [x] Realtime DTOs are internal app contracts.

### Task 9: Add DNSE Real WebSocket Market Stream

**Status:** Completed

**Description:** Replace the mock-only realtime source with an opt-in DNSE WebSocket market stream that subscribes to real DNSE quote channels, normalizes incoming messages into app-owned quote update DTOs, and broadcasts them through the existing SignalR quote hub. REST snapshot remains the initial board load and fallback path; DNSE WebSocket is used for intraday realtime updates.

**Acceptance criteria:**

- [x] DNSE WebSocket stream is selected by config, for example `MarketData:QuoteStream:SourceProvider = "DnseWebSocket"`.
- [x] Mock realtime remains available and is still the safe default when DNSE stream is not selected.
- [x] DNSE WebSocket credentials are read from configuration or `DNSE_API_KEY` / `DNSE_API_SECRET`.
- [x] WebSocket auth is isolated from REST signing and signs `api_key:timestamp:nonce` with HMAC-SHA256 hex.
- [x] Client connects to `wss://ws-openapi.dnse.com.vn/v1/stream?encoding=json` by default, unless overridden.
- [x] MVP subscribes to `security_definition.{boardId}.json`, `tick.{boardId}.json`, `top_price.{boardId}.json`, and `foreign.{boardId}.json`.
- [x] Incoming DNSE message types `sd`, `t`, `q`, and `f` map into `MarketQuoteUpdateDto` without leaking DNSE payload types outside Infrastructure.
- [x] Security definition updates can refresh reference, ceiling, floor, and trading status fields.
- [x] Trade updates can refresh last price, change, percent change, last quantity, total volume, total value, open, high, and low.
- [x] Quote updates can refresh bid/ask levels.
- [x] Foreign investor updates can refresh NN mua, NN ban, and room.
- [x] Client handles `ping`, `pong`, subscribe acknowledgements, `auth_success`, and `error` control messages.
- [x] Client reconnects with backoff, re-authenticates, and re-subscribes after connection loss.
- [x] Stream status is broadcast through the existing SignalR status DTO.
- [x] Frontend sends the currently opened market-board symbol list to the quote hub after each REST snapshot/filter change.
- [x] Backend deduplicates active symbols across SignalR connections and subscribes DNSE once per active symbol.
- [x] Quote updates are broadcast through symbol-scoped SignalR groups so clients receive updates for rows they currently display.
- [x] DNSE WebSocket connection is gated by active market-board demand and a configurable local streaming schedule.

**Verification:**

- [x] Unit tests cover WebSocket auth signature construction.
- [x] Unit tests cover channel name construction for security definition, trade, top price, foreign, and session.
- [x] Fixture tests cover DNSE `sd`, `t`, `q`, and `f` message mapping.
- [x] Unit tests cover per-symbol aggregation and change/percent calculation from reference price.
- [x] Unit tests cover active market-board subscription dedupe, connection removal, and change notification.
- [x] Unit tests cover DNSE WebSocket streaming schedule decisions.
- [x] Frontend tests cover sending snapshot symbols as a SignalR subscription request.
- [x] Unit tests cover DI/provider selection so mock and DNSE stream do not run at the same time.
- [x] `dotnet build`
- [x] `dotnet test`
- [ ] Manual verification with DNSE credentials: API logs connected/authenticated/subscribed and frontend receives SignalR updates.

**Dependencies:** Task 6, Task 7, Task 8

**Files likely touched:**

- `src/InvestView.Application/Dtos/MarketData/MarketQuoteUpdateDto.cs`
- `src/InvestView.Infrastructure/Dnse/*`
- `src/InvestView.Infrastructure/Realtime/*`
- `src/InvestView.Infrastructure/DependencyInjection.cs`
- `src/investview.web/src/shared/types/market.ts`
- `src/investview.web/src/features/market-board/*`
- `tests/InvestView.Api.Tests/Dnse/*`
- `tests/InvestView.Api.Tests/Realtime/*`

**Estimated scope:** Large, implemented as small backend-first increments.

## Milestone 4: Persistence, Watchlist, Portfolio, and Simulated Orders

Goal: add backend domain depth and the main investor workflow.

### Task 10: Add Persistence and Demo Auth Foundation

**Status:** Done

**Description:** Add EF Core SQL Server setup, initial entities, migrations, seed data, and demo JWT login.

**Implementation slices:**

- [x] Task 10.1 / Todo 8.1: Add EF Core SQL Server persistence foundation. This slice adds the core personal-data entities, `InvestViewDbContext`, SQL Server configuration, and initial migration. It intentionally does not add JWT/login behavior yet, and market-data REST endpoints plus `QuoteHub` remain public.
- [x] Task 10.2 / Todo 8.2: Add demo auth JWT, seed demo user/cash, and protect only personal-data APIs.

**Acceptance criteria:**

- [x] SQL Server provider is configured.
- [x] Initial schema supports users, watchlists, cash accounts, holdings, orders, and executions.
- [x] Demo user can log in and receive JWT.
- [x] Secrets and connection strings are environment-based.
- [x] Viewing the market board and quote realtime stream remains anonymous/public after auth is introduced.

**Verification:**

- [x] EF mapping tests or integration tests cover important relationships.
- [x] API test covers demo login.
- [x] `dotnet build`
- [x] `dotnet test`

**Dependencies:** Task 1

**Files likely touched:**

- `src/InvestView.Domain/*`
- `src/InvestView.Infrastructure/Data/*`
- `src/InvestView.Api/Controllers/AuthController.cs`
- `src/InvestView.Api/Program.cs`
- `tests/InvestView.Api.Tests/*`

**Estimated scope:** Medium

**Implemented notes:** Demo auth exposes `POST /api/auth/demo-login` and protected `GET /api/me`. Startup seeding creates the demo user plus a VND cash account when `DemoAuth:SeedOnStartup` is enabled. The JWT signing key is read from `Jwt:SigningKey` or `INVESTVIEW_JWT_SIGNING_KEY`; when omitted, the API uses an ephemeral development key for the current process. Market data endpoints and `/hubs/quotes` remain anonymous.

### Task 11: Add Watchlist Flow

**Status:** Done

**Description:** Implement watchlist REST endpoints and frontend watchlist interactions using authenticated demo user state.

**Acceptance criteria:**

- [x] User can list watchlist symbols.
- [x] User can add a valid symbol.
- [x] User can remove a symbol.
- [x] Duplicate watchlist items are handled predictably.
- [x] Frontend can add/remove from market board or watchlist panel.

**Verification:**

- [x] API tests cover add/remove/list.
- [x] Frontend tests cover watchlist interactions.
- [x] `dotnet test`
- [ ] `npm run test`
- [ ] Manual browser check.

**Dependencies:** Task 5, Task 10

**Files likely touched:**

- `src/InvestView.Application/Watchlists/*`
- `src/InvestView.Api/Controllers/WatchlistController.cs`
- `src/InvestView.Infrastructure/Data/*`
- `src/investview.web/src/features/watchlist/*`
- `src/investview.web/src/features/market-board/*`

**Estimated scope:** Medium

**Implemented notes:** Added protected `GET /api/watchlist`, `POST /api/watchlist`, and `DELETE /api/watchlist/{boardId}/{symbol}` endpoints backed by `WatchlistItems`. Adds are normalized, validated against the market data provider, and duplicate adds return the existing item instead of creating another row. The frontend exposes a watchlist panel under "Danh muc cua toi" that performs demo login, lists items, and adds/removes G1 symbols using the bearer token.

**Verification notes:** `dotnet test InvestView.sln -c Release`, `npm run test -- WatchlistPanel.test.tsx`, and `npm run build` pass. Full `npm run test` is still open because existing dirty changes outside Task 11 make `MarketBoard.test.tsx` expect stale labels/buttons in `MarketIndexOverview.tsx` and `SymbolDetailPanel.tsx`.

### Task 12: Add Simulated Order and Portfolio Flow

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

**Dependencies:** Task 10, Task 11

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

### Task 13: Expand DNSE REST Adapter for Symbol Detail, OHLC, and Latest Trades

**Status:** Done

**Description:** Expand the DNSE REST integration beyond the market-board snapshot to cover symbol detail, OHLC chart data, and latest matched trades needed by later detail/chart screens. Use the local SDK and DNSE docs as the source of truth for endpoint behavior, payload shape, and mapping edge cases.

**Acceptance criteria:**

- [x] API exposes symbol detail through `GET /api/market/symbols/{symbol}` with optional `boardId`.
- [x] Symbol detail maps DNSE instrument and security definition data into `SymbolDetailDto`.
- [x] Symbol detail includes the same snapshot fields needed by the market-board row: last price, change, change percent, matched quantity, accumulated volume/value, bid/ask levels, foreign buy/sell/room, open/high/low, and trading status.
- [x] Symbol detail includes extra instrument/security fields: ISIN, product group, security group, admin status, trading method status, trading sanction status, listing date, final trade date, and open interest quantity where DNSE provides them.
- [x] Symbol detail composes DNSE `/instruments`, `/price/{symbol}/secdef`, `/price/{symbol}/trades/latest`, `/price/{symbol}/quotes/latest`, and `/price/{symbol}/foreign-trading`.
- [x] API exposes chart data through `GET /api/market/symbols/{symbol}/ohlc`.
- [x] OHLC maps DNSE `/price/ohlc` data into `OhlcBarDto`.
- [x] OHLC query sends `type=STOCK`, `symbol`, `resolution`, and optional Unix `from`/`to`.
- [x] API exposes latest matched trades table data through `GET /api/market/symbols/{symbol}/trades/latest`.
- [x] Latest matched trades table uses DNSE `/price/{symbol}/trades` with `boardId`, bounded `limit`, and `order=DESC` so the UI can show multiple newest rows.
- [x] Latest matched trade rows map price, changed value/percent, matched quantity, accumulated volume/value, side, and time into app-owned `MarketTradeDto`.
- [x] Trade quantities and OHLC volumes are normalized with the configured quantity scale factor to match market-board display units.
- [x] Existing market-board snapshot integration remains unchanged.
- [x] DNSE response models remain inside Infrastructure.

**Verification:**

- [x] Unit tests cover symbol detail, OHLC, and latest matched trade mapping using fixture JSON.
- [x] Unit tests cover URL/query construction for symbol detail source endpoints.
- [x] Unit tests cover URL/query construction for `/price/ohlc`.
- [x] Unit tests cover URL/query construction for `/price/{symbol}/trades`.
- [x] API tests cover chart and latest matched trade endpoints with mock provider.
- [ ] Integration path can be manually verified when credentials are available.
- [x] `dotnet build`
- [x] `dotnet test`

**Dependencies:** Task 6

**Files likely touched:**

- `src/InvestView.Infrastructure/Dnse/*`
- `src/InvestView.Infrastructure/MarketData/*`
- `src/InvestView.Api/Program.cs`
- `tests/InvestView.Application.Tests/*` or `tests/InvestView.Api.Tests/*`

**Estimated scope:** Medium

### Task 14: Harden DNSE WebSocket Adapter

**Status:** In Progress

**Description:** Extend the DNSE WebSocket stream added in Task 9 with broader production hardening after the core investor workflow is stable. Use the local SDK WebSocket implementation as the reference for auth, channel names, message types, heartbeat, reconnect behavior, and performance tuning.

**Acceptance criteria:**

- [x] Optional session state uses `session.{productGroupId}.{boardId}.json` only when needed by active market-board subscriptions.
- [ ] Adapter tracks stream health: connected, authenticated, last pong, last message time, reconnect count, and active subscriptions.
- [x] Stream can subscribe dynamically based on active board symbols instead of only configured symbols.
- [x] Stream only connects when active subscriptions exist and the configured streaming schedule is open.
- [ ] Per-symbol update ordering is preserved under concurrent message load.
- [ ] Message parsing supports `msgpack` if realtime traffic requires it.

**Verification:**

- [x] Unit tests cover dynamic subscription changes and unknown message tolerance.
- [ ] Load-oriented tests cover high-frequency trade/quote messages.
- [x] Fixture tests cover session `s`.
- [ ] Manual verification with credentials if available.
- [ ] `dotnet build`
- [ ] `dotnet test`
- [ ] Manual browser check: frontend receives updates through SignalR.

**Dependencies:** Task 9, Task 13

**Files likely touched:**

- `src/InvestView.Infrastructure/Dnse/*`
- `src/InvestView.Infrastructure/Realtime/*`
- `src/InvestView.Api/Program.cs`
- `tests/InvestView.Application.Tests/*` or `tests/InvestView.Api.Tests/*`

**Estimated scope:** Medium

### Task 15: Harden Market Board Realtime Merge Rules

**Status:** In Progress

**Description:** Harden frontend realtime quote merging so the market board does not regress or display inconsistent values when DNSE WebSocket events arrive out of order, after reconnects, or from different channels with partial payloads. This task should be implemented after the realtime data path has been manually observed with enough live DNSE traffic.

**Acceptance criteria:**

- [ ] `marketBoardRealtime.ts` ignores stale updates that are older than the current row state.
- [ ] Merge rules are explicit per channel/field group: trade, top price, security definition, and foreign trading.
- [x] Partial realtime updates preserve unrelated snapshot or stream fields.
- [x] `change` and `changePercent` remain consistent with last price and reference price when DNSE omits either value.
- [x] Flash classes are applied only to cells whose displayed values actually changed.

**Verification:**

- [ ] Frontend unit tests cover stale update rejection.
- [ ] Frontend unit tests cover same-symbol partial updates arriving in mixed channel order.
- [ ] Frontend unit tests cover reconnect/replay-like older updates.
- [ ] Frontend unit tests cover bid/ask-only, trade-only, security-definition-only, and foreign-only updates.
- [ ] `npm run test`
- [ ] `npm run build`
- [ ] Manual browser check with realtime enabled during a trading window.

**Dependencies:** Task 9, Task 14, manual observation of live DNSE WebSocket behavior

**Files likely touched:**

- `src/investview.web/src/features/market-board/marketBoardRealtime.ts`
- `src/investview.web/src/features/market-board/marketBoardRealtime.test.ts`
- `src/investview.web/src/features/market-board/marketBoardFormatters.ts`
- `src/investview.web/src/shared/types/market.ts`

**Estimated scope:** Medium

### Task 16: Add Market Index Overview

**Status:** Done

**Description:** Add a market index overview section above the market board, using DNSE official index enum names directly. The feature shows compact intraday cards and a summary table for market indices such as `VNINDEX`, `VN30`, `HNX`, `HNX30`, and `UPCOM`. Index chart data uses DNSE OHLC for `type=INDEX`; realtime headline/breadth data uses the DNSE `market_index.{market_index}.json` WebSocket channel when enabled.

**Acceptance criteria:**

- [x] Backend exposes `GET /api/market/indices` for index summary snapshots.
- [x] Backend exposes `GET /api/market/indices/{indexName}/ohlc` for index OHLC chart data.
- [x] Index names use DNSE enum values directly, without SSI display-name mapping.
- [x] DNSE OHLC query for index uses `/price/ohlc` with `type=INDEX`, `symbol`, `resolution`, and optional Unix `from`/`to`.
- [x] DNSE WebSocket mapper handles `market_index` payloads and maps value, change, percent, breadth, volume, value, high/low, reference, market, session, and timestamp into app-owned DTOs.
- [x] SignalR broadcasts market index updates to frontend clients.
- [x] Frontend renders compact index cards with mini line/volume chart, current value, change, percent, volume/value, and breadth.
- [x] Frontend renders an index summary table with point, change, volume, value, and advance/decline columns.
- [x] UI uses the existing market-board dark workstation style and does not interfere with board filtering or symbol detail panel.

**Verification:**

- [x] Backend tests cover index snapshot endpoint and index OHLC endpoint with mock provider.
- [x] DNSE tests cover `type=INDEX` OHLC query construction.
- [x] DNSE WebSocket mapper fixture test covers market index payload.
- [x] Frontend tests cover rendering index cards/table and realtime index update merge.
- [x] `dotnet build`
- [x] `dotnet test`
- [x] `npm run test`
- [x] `npm run build`

**Dependencies:** Task 9, Task 13

**Files likely touched:**

- `src/InvestView.Application/Dtos/MarketData/*`
- `src/InvestView.Application/Abstractions/MarketData/IMarketDataProvider.cs`
- `src/InvestView.Api/Controllers/MarketController.cs`
- `src/InvestView.Api/Hubs/*`
- `src/InvestView.Infrastructure/Dnse/*`
- `src/InvestView.Infrastructure/MarketData/*`
- `src/InvestView.Infrastructure/Realtime/*`
- `src/investview.web/src/features/market-index/*`
- `src/investview.web/src/features/market-board/MarketBoard.tsx`
- `src/investview.web/src/shared/api/marketApi.ts`
- `src/investview.web/src/shared/realtime/useQuoteHubConnection.ts`
- `src/investview.web/src/shared/types/market.ts`
- `tests/InvestView.Api.Tests/*`

**Estimated scope:** Large, implemented as backend contract plus frontend rendering increments.

### Task 17: Implement Redis-Backed Realtime Market State Layer

**Status:** Implemented

**Description:** Replace the market-board freshness model from REST refresh/TTL caching to a production-grade latest-state pipeline. DNSE REST creates baseline snapshots only when local and Redis state are missing. DNSE WebSocket updates are written to Redis latest-state and published through Redis Pub/Sub; every API server consumes the same Pub/Sub event path, updates its local in-memory mirror, and broadcasts SignalR updates to its connected clients. EOD persistence is intentionally out of scope for this task.

**Acceptance criteria:**

- [x] Introduce app-owned abstractions for latest market state storage, event publishing, and local mirror reads.
- [x] DNSE REST snapshot path writes baseline quotes/indexes/trades into latest-state storage when state is missing.
- [x] DNSE WebSocket update path writes merged latest state to Redis and publishes an internal market event.
- [x] All SignalR broadcasts are triggered from the internal event subscriber path, including the server that originally received the DNSE update.
- [x] API snapshot reads use local memory first, Redis second, and DNSE REST only as fallback/backfill.
- [x] Partial quote/trade/index updates merge without overwriting existing fields with missing/null payload fields.
- [x] Local mirror can warm from Redis on cache miss or server start.
- [x] Redis Pub/Sub is used for low-latency fan-out; Redis Streams/EOD persistence are documented as future work and not implemented in this task.
- [x] Existing frontend contracts remain stable; frontend still receives REST snapshots and SignalR deltas.

**Verification:**

- [x] Unit tests cover partial update merge rules and stale update rejection by timestamp/sequence where available.
- [x] Unit tests cover local -> shared state -> DNSE fallback order.
- [x] Unit tests cover event publisher/subscriber path and ensure broadcast is not performed directly from DNSE handler.
- [x] Integration-style tests cover two simulated app-server mirrors receiving the same published update.
- [x] `dotnet build`
- [x] `dotnet test`
- [ ] `npm run test` - not run; task did not change frontend code.
- [ ] `npm run build` - not run; task did not change frontend code.

**Dependencies:** Task 7, Task 8, Task 13, Task 16

**Files likely touched:**

- `src/InvestView.Application/Abstractions/MarketData/*`
- `src/InvestView.Application/Abstractions/Realtime/*`
- `src/InvestView.Infrastructure/MarketData/*`
- `src/InvestView.Infrastructure/Realtime/*`
- `src/InvestView.Infrastructure/Dnse/*`
- `src/InvestView.Infrastructure/DependencyInjection.cs`
- `src/InvestView.Api/Hubs/*`
- `src/InvestView.Api/appsettings.json`
- `tests/InvestView.Api.Tests/*`

**Implemented notes:** The production runtime path is Redis-only. `MarketData:State:RedisConnectionString` or `REDIS_CONNECTION_STRING` is required; in-memory state remains only as each API server's local hot mirror and test infrastructure. EOD persistence and Redis Streams remain future work.

**Follow-up note:** The older process-local cache decorator and its configuration were removed. REST fallback/backfill now goes through the Redis-backed state provider path instead of a separate local cache decorator.

### Task 18: Implement Redis Market Data Schema V2

**Status:** Implemented

**Description:** Organize Redis market data by canonical subject so REST snapshots, REST detail/OHLC backfill, and realtime updates all converge into Redis before API reads. Quote/detail/index state uses Hashes, latest trades use Lists, OHLC uses Sorted Sets with range coverage, and board/market/index memberships use Sets.

**Acceptance criteria:**

- [x] Redis keys include prefix, environment, market-data namespace, and schema version.
- [x] Quote state is stored once per `boardId:symbol` and not duplicated per board UI snapshot.
- [x] Quote Hash includes canonical payload, scalar fields, and group update timestamps.
- [x] Symbol detail and OHLC REST reads use local mirror -> Redis -> fallback/backfill order.
- [x] Symbol detail can build its initial price/depth/foreign snapshot from Redis quote state so opening a detail panel does not call REST just to re-fetch market-board fields.
- [x] Symbol detail metadata backfill is separated from full price snapshot fallback; DNSE metadata-only reads call `/instruments` and `/price/{symbol}/secdef` without calling trade, top-price, or foreign-trading endpoints when quote state is present.
- [x] Market-board filters resolve Redis membership and only REST backfill missing symbols.
- [x] OHLC cache hits require a matching coverage token before returning range data.
- [x] TTLs are separated by quote state, symbol detail, latest trades, OHLC, and membership data.
- [x] Tests cover Redis key/field schema and provider fallback/backfill behavior.

**Verification:**

- [x] `dotnet build`
- [x] `dotnet test`

**Dependencies:** Task 17

**Files likely touched:**

- `src/InvestView.Application/Abstractions/MarketData/IMarketStateStore.cs`
- `src/InvestView.Infrastructure/MarketData/*`
- `src/InvestView.Api/appsettings.json`
- `tests/InvestView.Api.Tests/MarketData/*`
- `SPEC.md`
- `docs/decisions/ADR-004-redis-backed-market-state.md`

### Task 19: Expand DNSE WebSocket Market-State Updates

**Status:** Completed

**Description:** Subscribe to the remaining DNSE market-data WebSocket channels and route every supported realtime payload through Redis market-state before local mirrors and SignalR fan-out. This closes the gap where quote/depth/foreign data is realtime but OHLC, expected auction price, estimated index, and session state still depend mostly on REST backfill.

**Acceptance criteria:**

- [x] DNSE channel builder supports `ohlc.{resolution}`, `ohlc_closed.{resolution}`, `expected_price.{boardId}`, `estimated_market_index.{indexName}`, and `session.{productGroupId}.{boardId}`.
- [x] DNSE message mapper handles `b`, `bc`, `e`, `emi`, and `s` payloads in addition to the existing `sd`, `t`, `te`, `q`, `f`, and `mi` handlers.
- [x] OHLC realtime updates upsert Redis OHLC sorted sets for symbols and market indices without waiting for REST.
- [x] Expected auction price updates enrich quote state without overwriting matched price.
- [x] Estimated market index updates enrich index state without overwriting the actual published index value.
- [x] Session updates are stored in Redis as board/session state.
- [x] Redis event publication remains the internal synchronization path; DNSE handlers do not broadcast directly to clients.
- [x] Unit tests cover channel names, message mapping, Redis/in-memory state writes, and provider reads where applicable.

**Verification:**

- [x] `dotnet build`
- [x] `dotnet test`

**Dependencies:** Task 18

**Files likely touched:**

- `src/InvestView.Application/Dtos/MarketData/*`
- `src/InvestView.Application/Abstractions/MarketData/IMarketStateStore.cs`
- `src/InvestView.Application/Abstractions/Realtime/IMarketStateEventPublisher.cs`
- `src/InvestView.Infrastructure/Dnse/*`
- `src/InvestView.Infrastructure/Realtime/*`
- `src/InvestView.Infrastructure/MarketData/*`
- `docs/plans/implementation-plan.md`
- `tests/InvestView.Api.Tests/*`

**Implemented notes:** The WebSocket stream now subscribes to expected auction price, open/closed OHLC for configured resolutions, estimated market indices, and session state. These updates are normalized into app-owned DTOs, written through the shared market-state abstraction, published as internal market events, and then applied by local mirrors. Realtime OHLC bars are upserted into sorted sets, but OHLC range coverage still comes from REST backfill so chart APIs do not treat partial realtime history as a full cache hit.

### Task 20: Add Proactive Security Definition Warmup

**Status:** Done

**Description:** Add a separate BOD warmup path for DNSE `security_definition.{boardId}.json` so reference, ceiling, floor, and security status can be loaded into Redis for HOSE/HNX/UPCOM before the market board has active SignalR subscriptions. This warmup must not depend on `QuoteStream:Schedule:RequireActiveSubscriptions`.

**Acceptance criteria:**

- [x] Warmup resolves stock symbols for configured market ids (`STO`, `STX`, `UPX`) through DNSE `/instruments` with `securityGroupId=ST`.
- [x] Warmup writes complete market membership sets into Redis for each configured market id.
- [x] Warmup connects to DNSE WebSocket during a configurable local BOD window and subscribes only to `security_definition.{boardId}.json`.
- [x] Warmup batches symbol subscriptions to avoid sending one huge subscribe payload.
- [x] Incoming `sd` payloads are mapped through the existing DNSE message mapper and published through the market-state event pipeline.
- [x] Redis quote state is updated through the existing merge path, preserving other fields when security-definition fields arrive.
- [x] Warmup runs at most once per configured local trading day and skips cleanly when disabled or DNSE credentials are missing.

**Verification:**

- [x] Unit tests cover symbol resolution, market filters, paging, and dedupe.
- [x] Unit tests cover warmup schedule decisions.
- [x] Targeted backend tests cover DNSE websocket mapper/builder and DI.
- [x] `dotnet test tests/InvestView.Api.Tests/InvestView.Api.Tests.csproj -c Release --filter "FullyQualifiedName~DependencyInjectionTests|FullyQualifiedName~DnseWebSocketSubscriptionBuilderTests|FullyQualifiedName~DnseWebSocketMessageMapperTests|FullyQualifiedName~SecurityDefinitionWarmup"`

**Dependencies:** Task 17, Task 18, Task 19

**Files likely touched:**

- `src/InvestView.Infrastructure/Realtime/*`
- `src/InvestView.Infrastructure/DependencyInjection.cs`
- `src/InvestView.Api/appsettings.json`
- `tests/InvestView.Api.Tests/Realtime/*`
- `docs/plans/implementation-plan.md`
- `docs/plans/todo.md`

### Task 21: Add Docker Compose and Demo Documentation

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

**Dependencies:** Task 12

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
| DNSE credentials are unavailable | Medium | Keep mock provider as default and make DNSE provider opt-in by config. |
| DNSE payload shape changes | Medium | Keep DNSE models in Infrastructure and map to internal DTOs. |
| Foundation becomes too large | High | Milestone 1 only creates walking skeleton, not full domain/database. |
| Realtime adds complexity before UI is stable | Medium | Build REST market board first, then add SignalR. |
| Trading domain causes broad rewrites | High | Add order/portfolio only after market contracts and persistence are stable. |
| Docker slows early development | Medium | Add Docker Compose after core workflow, except minimal config if needed for SQL Server. |

## Open Questions

1. Will DNSE credentials be available during implementation, or should DNSE tasks stay behind fixture tests until later?
2. Should demo auth be seeded-login only, or should simple registration be included?
3. Should the initial market board focus only on Vietnamese symbols from DNSE?

## Plan Control

- Do not start a later milestone if the current checkpoint fails.
- Do not introduce Redis, Kubernetes, real order submission, or admin workflows without updating `SPEC.md`.
- Do not change app-owned market DTOs just to match DNSE payloads. Map DNSE into the app contracts instead.
- Keep each implementation commit tied to one task.
