# InvestView

InvestView is a personal fullstack securities web app that explores how a market-data driven trading workstation can be built with modern web, backend, realtime, caching, testing, and deployment practices.

The project focuses on a realistic but safe scope: market board, index overview, watchlist, portfolio, simulated orders, realtime quote updates, Redis-backed market state, SQL Server persistence, and Docker-based deployment. Trading is simulated only; the application never submits real brokerage orders.

## Status

InvestView is an active personal project. The core MVP slices are implemented: React market board, ASP.NET Core API, provider-neutral market data boundary, mock provider, optional external provider adapters, SignalR realtime updates, Redis market state, SQL Server-backed demo account data, simulated trading flows, and Docker Compose packaging.

The project is not affiliated with any brokerage, data provider, or employer. It does not clone proprietary trading systems. External market data providers are used only behind backend-owned adapters.

## What This Demonstrates

| Area | Implementation in InvestView |
|---|---|
| Fullstack product thinking | React workstation UI, REST API, SignalR, SQL Server, Redis, Docker |
| Securities domain modeling | Quotes, bid/ask depth, indices, watchlists, cash, holdings, simulated orders, executions |
| Realtime engineering | Market data provider stream -> backend normalization -> Redis Pub/Sub -> SignalR -> React |
| Caching and state design | Redis latest-state store, local hot mirrors, subject-based keys, REST fallback only on cache miss |
| Backend architecture | Modular monolith with API, Application, Domain, and Infrastructure projects |
| Frontend architecture | Feature folders, typed API clients, AG Grid market board, isolated realtime client |
| Test-driven design | Domain, API, provider adapter mapping/signing, Redis fallback/merge, and React formatting/realtime tests |
| Deployment readiness | Dockerfiles, Docker Compose, Nginx-ready forwarded headers, environment-based configuration |
| Security boundaries | Provider credentials stay on the backend; simulated trading is explicitly separated from real order routing |

## Core Features

- Demo login with JWT authentication.
- Dense Vietnamese securities-style market board using AG Grid.
- Market index overview with index cards, table, OHLC data, and realtime updates.
- Realtime quote updates through SignalR.
- Watchlist groups and symbol watchlist management.
- Symbol detail panel with quote, depth, latest trades, and OHLC data.
- Simulated buy/sell order ticket.
- Portfolio view with cash, holdings, order history, and simulated execution effects.
- Provider-agnostic market data boundary with mock data for local demos.
- Optional real market-data provider integration through backend adapters.
- Redis-backed latest market state with Pub/Sub fan-out.
- SQL Server persistence for demo user, watchlists, holdings, orders, and executions.
- Docker Compose stack for local or single-VPS deployment.

## Architecture

```text
Browser
  |
  | React + TypeScript SPA
  | AG Grid market board, SignalR client, React Query, feature modules
  v
Web container
  |
  | Nginx serves static assets and proxies /api, /health, /hubs
  v
ASP.NET Core API
  |
  | REST controllers, JWT auth, SignalR QuoteHub, OpenAPI, forwarded headers
  v
Application layer
  |
  | Provider-neutral DTOs and use-case interfaces
  v
Domain layer                         Infrastructure layer
  |                                  |
  | Trading rules                    | EF Core + SQL Server
  | Cash, holdings, orders           | Redis market state
  | Watchlists, executions           | Market data provider adapters
  |                                  | Mock provider + optional external providers
  v                                  v
SQL Server                        Redis
                                   ^
                                   |
Market data providers -------------+
  - Mock snapshot/stream
  - Optional external REST/WebSocket adapters
```

### Backend Projects

| Project | Responsibility |
|---|---|
| `InvestView.Api` | HTTP endpoints, authentication, SignalR hub, OpenAPI, middleware |
| `InvestView.Application` | App-owned contracts, DTOs, service interfaces, use-case boundaries |
| `InvestView.Domain` | Trading entities and rules: cash, holdings, orders, executions, watchlists |
| `InvestView.Infrastructure` | EF Core, SQL Server, Redis, provider adapters, mock providers, realtime services |

The backend follows one important rule: application, API, domain, and frontend code depend on InvestView-owned contracts, not external provider payloads. Provider-specific models and protocol details stay inside `InvestView.Infrastructure`.

### Frontend Structure

```text
src/investview.web/src/
  app/                 # app shell, providers, routing composition
  features/
    auth/              # demo session state and controls
    market-board/      # AG Grid board, quote mapping, realtime merge
    market-index/      # index cards, index table, realtime index updates
    order-ticket/      # simulated order entry
    portfolio/         # cash, holdings, portfolio summary
    symbol-detail/     # quote detail, OHLC chart, latest trades
    system-status/     # health/realtime status
    trading/           # trading drawer workflow
    watchlist/         # watchlist groups and symbol management
  shared/
    api/               # typed API clients
    realtime/          # SignalR connection
    types/             # shared TypeScript contracts
    ui/                # reusable UI primitives
```

The UI is intentionally built as an operational securities tool, not a marketing landing page. The market board emphasizes dense scanning, stable columns, price colors, horizontal scrolling, and incremental row updates.

## Realtime and Caching Design

InvestView treats market data as a provider-agnostic pipeline. The app can run from a mock provider for local demos, or from an optional real provider through the same application contracts. REST is used for baseline and backfill; it is not the normal freshness path for the market board. The realtime/latest path is Redis-backed:

```text
Market data provider stream
  -> normalize provider payloads into InvestView DTOs
  -> merge partial updates into latest market state
  -> write Redis latest-state keys
  -> publish Redis Pub/Sub market event
  -> every API server updates its local hot mirror
  -> every API server broadcasts SignalR updates to connected browser clients
```

Snapshot reads use this order:

1. Local in-memory hot mirror.
2. Redis latest-state store.
3. Configured provider snapshot/backfill only for missing state.

This design is useful because market boards can contain many symbols. Re-fetching full snapshots from an upstream provider during the trading session wastes rate limit budget and creates inconsistent freshness. Redis gives the application a shared latest-state layer that can support more than one API server later.

### Redis Subjects

Redis keys are organized by market-data subject, not by UI page:

```text
quote:{boardId:symbol}:state
quote:{boardId:symbol}:trades
security:{boardId:symbol}:detail
ohlc:{symbol}:{resolution}
index:{indexName}:state
session:{productGroupId:boardId}:state
board:{boardId}:symbols
market:{marketId}:symbols
category:{indexName}:symbols
```

This lets the market board, symbol detail panel, watchlists, index overview, and chart APIs reuse the same state instead of each feature creating its own disconnected cache.

## Business Rules

InvestView models a demo investor account:

- Buy orders require enough simulated cash before acceptance.
- Sell orders require enough available holdings before acceptance.
- Orders can be pending, open, filled, partially filled, cancelled, or rejected.
- Simulated executions update cash, holdings, and portfolio value.
- Every order and execution is demo-only.
- Real brokerage order submission is out of scope by design.

The domain model exists so the project is not only a market-data UI. It also demonstrates backend rules, transaction boundaries, persistence, and testable financial workflows.

## API Overview

| Area | Endpoint |
|---|---|
| Health | `GET /health` |
| Auth | `POST /api/auth/demo-login` |
| Current user | `GET /api/me` |
| Market board | `GET /api/market/quotes` |
| Market session | `GET /api/market/session` |
| Symbol detail | `GET /api/market/symbols/{symbol}` |
| Symbol OHLC | `GET /api/market/symbols/{symbol}/ohlc` |
| Latest trades | `GET /api/market/symbols/{symbol}/trades/latest` |
| Indices | `GET /api/market/indices` |
| Index OHLC | `GET /api/market/indices/{indexName}/ohlc` |
| Watchlist | `GET /api/watchlist`, `POST /api/watchlist`, `POST /api/watchlist/{groupId}/items`, `DELETE /api/watchlist/{groupId}/items/{boardId}/{symbol}` |
| Portfolio | `GET /api/portfolio` |
| Orders | `GET /api/orders`, `POST /api/orders`, `POST /api/orders/{orderId}/cancel` |
| Realtime | `/hubs/quotes` |

OpenAPI is enabled in development through the ASP.NET Core OpenAPI setup.

## Tech Stack

### Backend

- .NET 10 / ASP.NET Core Web API
- SignalR
- Entity Framework Core
- SQL Server 2022
- StackExchange.Redis
- JWT bearer authentication
- xUnit tests

### Frontend

- React 19
- TypeScript
- Vite
- AG Grid Community
- TanStack Query
- SignalR JavaScript client
- Tailwind CSS
- Lightweight Charts
- Vitest and React Testing Library

### Runtime and Deployment

- Docker Compose
- Nginx web container for the Vite build
- Host-level Nginx reverse proxy friendly setup
- Redis for shared market state and Pub/Sub
- SQL Server persistent volume
- Environment-variable based configuration

## Getting Started With Docker

Docker Compose is the easiest way to run the full stack.

### 1. Create Environment File

```powershell
Copy-Item .env.example .env
```

Edit `.env` and set at least:

```env
SQL_SA_PASSWORD=replace-with-a-strong-sql-password
JWT_SIGNING_KEY=replace-with-at-least-32-characters-for-jwt-signing
INVESTVIEW_DEMO_PASSWORD=replace-with-demo-login-password
```

Keep mock market data enabled for the first run:

```env
MARKET_DATA_PROVIDER=Mock
QUOTE_STREAM_SOURCE_PROVIDER=Mock
SECURITY_DEFINITION_WARMUP_ENABLED=false
```

### 2. Build and Start

```powershell
docker compose --env-file .env build
docker compose --env-file .env up -d
```

Open:

```text
http://localhost:8080
```

The API is also exposed for host-local diagnostics:

```text
http://127.0.0.1:5122/health
```

### 3. Check Logs

```powershell
docker compose ps
docker compose logs -f api
docker compose logs -f web
```

### 4. Stop

```powershell
docker compose down
```

To remove local data volumes:

```powershell
docker compose down -v
```

## Market Data Providers

InvestView is provider-agnostic at the application boundary. Controllers, SignalR contracts, React components, and domain services work with InvestView DTOs instead of provider payloads.

The default provider is mock data, which keeps the full demo runnable without external credentials:

```env
MARKET_DATA_PROVIDER=Mock
QUOTE_STREAM_SOURCE_PROVIDER=Mock
SECURITY_DEFINITION_WARMUP_ENABLED=false
```

DNSE can be enabled as an optional real market-data provider through backend configuration:

```env
MARKET_DATA_PROVIDER=Dnse
QUOTE_STREAM_SOURCE_PROVIDER=DnseWebSocket
SECURITY_DEFINITION_WARMUP_ENABLED=true
DNSE_API_KEY=your-api-key
DNSE_API_SECRET=your-api-secret
```

Provider credentials must stay on the backend. The React app never calls an external market data provider directly and never receives provider API keys or secrets.

## Local Development

### Backend

```powershell
dotnet restore
dotnet build
dotnet test
dotnet run --project src/InvestView.Api
```

For local backend development, SQL Server and Redis must be reachable according to `src/InvestView.Api/appsettings.json` or environment overrides.

### Frontend

```powershell
Set-Location src/investview.web
npm install
npm run dev
```

Useful frontend commands:

```powershell
npm run build
npm run test
npm run lint
```

## Testing and Quality

InvestView is structured around testable boundaries:

- Domain tests cover cash, holdings, simulated order validation, order state transitions, and portfolio effects.
- API tests cover health, auth, market, watchlist, portfolio, and order flows.
- Provider adapter tests cover optional DNSE REST signing, URL construction, WebSocket channel construction, and payload mapping.
- Redis market-state tests cover merge behavior, stale update rejection, local -> Redis -> provider fallback, Pub/Sub propagation, and key schema.
- Frontend tests cover market board rendering, formatting, color rules, realtime merge behavior, index rendering, watchlist, portfolio, and order-ticket interactions.

Primary verification commands:

```powershell
dotnet test
Set-Location src/investview.web
npm run test
npm run build
```

Docker verification:

```powershell
docker compose --env-file .env config --quiet
docker compose --env-file .env build
docker compose --env-file .env up -d
```

## Security and Safety Boundaries

- No real order routing.
- No real-money trading.
- No brokerage credentials in frontend code.
- External provider API keys and secrets are backend-only environment values.
- Demo account credentials are environment-driven.
- The application uses simulated cash, simulated holdings, and simulated executions.
- The UI and docs must not present simulated performance as real investment performance.
- TLS is expected to terminate at a host reverse proxy in production/VPS deployments.

## Deployment Notes

The repository includes Docker packaging for a single-host deployment:

- `docker-compose.yml`
- `src/InvestView.Api/Dockerfile`
- `src/investview.web/Dockerfile`
- `src/investview.web/nginx/default.conf`
- `docs/deployment/docker.md`

The container layout is:

```text
Host Nginx / domain / TLS
  -> web container on WEB_HTTP_PORT
      -> static React app
      -> proxy /api, /health, /hubs to api:8080
  -> api container
      -> SQL Server
      -> Redis
```

The API processes forwarded headers before HTTPS redirection, so it can sit behind Nginx or another reverse proxy that sets `X-Forwarded-Proto` and `X-Forwarded-For`.

## Design Decisions

Architectural decisions are documented as ADRs:

- `docs/decisions/ADR-001-depend-on-internal-contracts.md`
- `docs/decisions/ADR-002-market-data-snapshot-plus-stream.md`
- `docs/decisions/ADR-003-use-ag-grid-for-market-board.md`
- `docs/decisions/ADR-004-redis-backed-market-state.md`

These explain why the project uses internal contracts, REST snapshot plus stream updates, AG Grid for the market board, and Redis-backed latest market state.

## Future Work

- Add CI for backend tests, frontend tests, Docker build, and Compose validation.
- Add production observability for provider health, Redis hit/miss, and realtime stream status.
- Strengthen stale-update handling for mixed realtime channel ordering and reconnect scenarios.
- Add VPS deployment automation for host-level Nginx, TLS, and update workflow.
- Consider Kubernetes or Docker Swarm only after the single-host Docker deployment is stable and documented.

## Repository Structure

```text
src/
  InvestView.Api/
  InvestView.Application/
  InvestView.Domain/
  InvestView.Infrastructure/
  investview.web/

tests/
  InvestView.Api.Tests/
  InvestView.Application.Tests/
  InvestView.Domain.Tests/

docs/
  decisions/
  deployment/
  plans/

automation/
deploy/
```

## License

No license has been selected yet. Treat the code as private unless a license is added.
