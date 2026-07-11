# InvestView MVP Todo

## Milestone 1: Scaffold and Walking Skeleton

- [x] Task 1: Scaffold backend solution
- [x] Task 2: Scaffold React app
- [x] Checkpoint: Walking skeleton

## Milestone 2: Market Data Contracts, Mock Provider, and Caching

- [x] Task 3: Add market data contracts and mock provider
  - [x] Shape DTOs for REST snapshot plus future WebSocket updates
  - [x] Include bid/ask level DTO and normalized timestamp fields
  - [x] Keep all contracts provider-neutral; no DNSE types outside Infrastructure
- [x] Task 4: Add cached market data provider
- [x] Task 5: Build market board UI with AG Grid Community
  - [x] Configure Tailwind CSS and apply `docs/design.md`
  - [x] Render grouped securities-board columns, not a generic quote table
  - [x] Show 3 bid levels, matched quote fields, and 3 ask levels
  - [x] Apply ceiling/floor/reference/increase/decrease color rules
  - [x] Keep API client, quote mapping, and grid component separated
  - [x] Keep Task 5 as REST snapshot only; SignalR updates come in Task 7
- [x] Checkpoint: Market board REST

## Milestone 3: Realtime Quote Updates

- [x] Task 6: Add SignalR quote hub with mock stream
  - [x] Establish app-facing realtime DTO before DNSE WebSocket adapter
- [x] Task 7: Connect frontend to realtime quotes
  - [x] Apply quote updates through AG Grid row identity and transactions
- [x] Checkpoint: Realtime market board

## Milestone 4: Persistence, Watchlist, Portfolio, and Simulated Orders

- [ ] Task 8: Add persistence and demo auth foundation
  - [x] Task 8.1: Add EF Core SQL Server persistence foundation
    - [x] Add core user, watchlist, cash, holding, order, and execution entities
    - [x] Add `InvestViewDbContext`, SQL Server configuration, and initial migration
    - [x] Keep market-data REST endpoints and quote hub public/anonymous
  - [ ] Task 8.2: Add demo auth JWT and seed demo user/cash
- [ ] Task 9: Add watchlist flow
- [ ] Task 10: Add simulated order and portfolio flow
- [ ] Checkpoint: Core investor workflow

## Milestone 5: DNSE Integration and Demo Packaging

- [x] Task 11: Add DNSE REST adapter
  - [x] Implement isolated REST signer from local SDK reference
  - [x] Cover `/instruments`, `/price/{symbol}/secdef`, latest trade, latest quote, and OHLC
  - [x] Keep TLS certificate validation enabled
- [x] Task 12: Add DNSE WebSocket adapter
  - [x] Implement separate WebSocket auth flow
  - [x] Subscribe to `tick.G1.json`, `top_price.G1.json`, and `security_definition.G1.json`
  - [x] Handle ping/pong, reconnect, re-auth, re-subscribe, and stream health
- [x] Task 13: Add proactive Security Definition warmup
  - [x] Resolve HOSE/HNX/UPCOM stock symbols via DNSE `/instruments`
  - [x] Store market memberships in Redis
  - [x] Subscribe only `security_definition.G1.json` during the BOD warmup window
  - [x] Publish `sd` updates through the Redis market-state merge pipeline
- [ ] Task 14: Add Docker Compose and demo documentation
- [ ] Checkpoint: MVP ready
