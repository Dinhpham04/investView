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
- [ ] Task 5: Build market board UI with AG Grid Community
  - [ ] Configure Tailwind CSS and apply `docs/design.md`
  - [ ] Render grouped securities-board columns, not a generic quote table
  - [ ] Show 3 bid levels, matched quote fields, and 3 ask levels
  - [ ] Apply ceiling/floor/reference/increase/decrease color rules
  - [ ] Keep API client, quote mapping, and grid component separated
  - [ ] Keep Task 5 as REST snapshot only; SignalR updates come in Task 7
- [ ] Checkpoint: Market board REST

## Milestone 3: Realtime Quote Updates

- [ ] Task 6: Add SignalR quote hub with mock stream
  - [ ] Establish app-facing realtime DTO before DNSE WebSocket adapter
- [ ] Task 7: Connect frontend to realtime quotes
  - [ ] Apply quote updates through AG Grid row identity and transactions
- [ ] Checkpoint: Realtime market board

## Milestone 4: Persistence, Watchlist, Portfolio, and Simulated Orders

- [ ] Task 8: Add persistence and demo auth foundation
- [ ] Task 9: Add watchlist flow
- [ ] Task 10: Add simulated order and portfolio flow
- [ ] Checkpoint: Core investor workflow

## Milestone 5: DNSE Integration and Demo Packaging

- [ ] Task 11: Add DNSE REST adapter
  - [ ] Implement isolated REST signer from local SDK reference
  - [ ] Cover `/instruments`, `/price/{symbol}/secdef`, latest trade, latest quote, and OHLC
  - [ ] Keep TLS certificate validation enabled
- [ ] Task 12: Add DNSE WebSocket adapter
  - [ ] Implement separate WebSocket auth flow
  - [ ] Subscribe to `tick.G1.json`, `top_price.G1.json`, and `security_definition.G1.json`
  - [ ] Handle ping/pong, reconnect, re-auth, re-subscribe, and stream health
- [ ] Task 13: Add Docker Compose and demo documentation
- [ ] Checkpoint: MVP ready
