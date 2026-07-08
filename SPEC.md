# Spec: InvestView

## Status

Accepted working spec.

Last updated: 2026-07-08.

## Source References

- FSS job description: https://fss.com.vn/truyen-dung/hn-lap-trinh-front-end-2/
- DNSE Market Data SDKs: https://developers.dnse.com.vn/docs/sdk/market_data/
- DNSE custom WebSocket client: https://developers.dnse.com.vn/docs/sdk/build_websocket/
- DNSE Market Data REST APIs: https://developers.dnse.com.vn/docs/dnse/market-data/
- DNSE market data WebSocket connection guide: https://developers.dnse.com.vn/docs/guide/market-data/connect
- DNSE market data enum guide: https://developers.dnse.com.vn/docs/guide/enum/market_data
- DNSE authentication guide: https://developers.dnse.com.vn/docs/guide/intro/authentication
- Local DNSE SDK reference: `docs/openapi-sdk-main/python/dnse/api/client.py`
- Local DNSE REST signing reference: `docs/openapi-sdk-main/python/dnse/api/common.py`
- Local DNSE WebSocket reference: `docs/openapi-sdk-main/python/dnse/websocket/client.py`
- Local DNSE WebSocket auth reference: `docs/openapi-sdk-main/python/dnse/websocket/auth.py`
- Local DNSE WebSocket model reference: `docs/openapi-sdk-main/python/dnse/websocket/models.py`
- AG Grid React data grid: https://www.ag-grid.com/react-data-grid/
- AG Grid high-frequency update guidance: https://www.ag-grid.com/react-data-grid/data-update/

## Assumptions

1. InvestView is a portfolio project for applying to the FSS fullstack securities role.
2. The project should demonstrate balanced fullstack ability, not frontend-only work.
3. The main demo user is an individual investor using a simulated portfolio.
4. The backend is ASP.NET Core/C# because financial systems benefit from explicit domain rules, validation, typed contracts, logging, and testability.
5. The frontend is React with TypeScript because the job description emphasizes ReactJS, JavaScript ES6+, UI/UX, and realtime data handling.
6. DNSE is the real third-party market data provider. DNSE credentials must stay on the backend.
7. Trading inside InvestView is simulated only. The system must never submit real orders to DNSE or any brokerage.
8. The MVP should show fewer features done well, with each feature mapped to skills from the job description: React UI/UX, REST API, server-side integration, caching, realtime data, Docker, and securities domain understanding.

## Objective

Build a fullstack stock investing web application that can be demoed in an interview for a fresher/junior fullstack securities role.

The application lets a demo investor:

- sign in with a demo account,
- view a market board fed by DNSE market data or local mock data,
- receive realtime quote updates,
- manage a watchlist,
- place and cancel simulated buy/sell orders,
- see cash, holdings, portfolio value, and order history.

Success means the project tells a clear technical story:

- React renders a dense, responsive, securities-style UI that handles live market updates without noisy re-renders.
- ASP.NET Core exposes clean REST APIs, SignalR realtime endpoints, Swagger documentation, validation, logging, and tests.
- SQL Server stores demo investor state: users, watchlists, cash, holdings, orders, executions, and relevant market snapshots.
- DNSE integration is isolated behind backend adapters, with caching and mock fallback.
- Docker Compose can run the local demo stack.

## Tech Stack

- Backend: ASP.NET Core Web API, C#/.NET 8 or newer.
- Realtime outbound to frontend: SignalR.
- Third-party market data: DNSE REST APIs and DNSE WebSocket.
- Frontend: React with TypeScript.
- Styling: Tailwind CSS using the InvestView design standard in `docs/design.md`.
- Market board grid: AG Grid Community for grouped, pinned, high-density quote display.
- Database: SQL Server.
- Data access: Entity Framework Core.
- Authentication: JWT-based demo auth.
- Caching: ASP.NET Core `IMemoryCache` for MVP.
- Testing: xUnit for backend; Vitest and React Testing Library for frontend.
- API documentation: OpenAPI/Swagger.
- Local runtime: Docker Compose for API, web, and SQL Server.

## Commands

These commands are targets for the future scaffold and may need path adjustment after projects are created.

```powershell
# Repository checks
node scripts/validate-codex-setup.js
node scripts/validate-skills.js

# Backend
dotnet restore
dotnet build
dotnet test
dotnet run --project src/InvestView.Api

# Frontend
cd src/investview.web
npm install
npm run dev
npm run build
npm run test

# Local demo stack
docker compose up --build
docker compose down
```

## Project Structure

```text
src/
  InvestView.Api/
    Controllers/
    Hubs/
    Middleware/
    Program.cs

  InvestView.Application/
    Abstractions/
    Dtos/
    Market/
    Orders/
    Portfolio/
    Watchlists/

  InvestView.Domain/
    CashAccounts/
    Holdings/
    MarketData/
    Orders/
    Users/

  InvestView.Infrastructure/
    Data/
    Dnse/
    MarketData/
    Realtime/
    SeedData/

  investview.web/
    src/
      app/
      features/
        auth/
        market-board/
        order-ticket/
        portfolio/
        watchlist/
      shared/
        api/
        realtime/
        types/
        ui/
        utils/

tests/
  InvestView.Api.Tests/
  InvestView.Application.Tests/
  InvestView.Domain.Tests/
  investview.web.tests/

docs/
  architecture/
  decisions/
  plans/
  specs/
```

## System Architecture

InvestView is a modular monolith backend with a React SPA frontend.

Architecture decisions:

- ADR-001: `docs/decisions/ADR-001-depend-on-internal-contracts.md`
- ADR-002: `docs/decisions/ADR-002-market-data-snapshot-plus-stream.md`
- ADR-003: `docs/decisions/ADR-003-use-ag-grid-for-market-board.md`

```text
DNSE REST API / DNSE WebSocket
        |
        v
InvestView.Infrastructure
  DnseMarketDataClient
  DnseWebSocketClient
  CachedMarketDataProvider
  MockMarketDataProvider
        |
        v
InvestView.Application
  MarketBoardService
  OrderService
  PortfolioService
  WatchlistService
        |
        v
InvestView.Api
  REST Controllers
  QuoteHub (SignalR)
  Swagger
        |
        v
React frontend
```

Core rule: the frontend never calls DNSE directly. All DNSE credentials, retries, caching, data normalization, and logging stay in the backend.

Core dependency rule: application, domain, API, and frontend code depend on InvestView-owned contracts, not DNSE payloads or SDK types. DNSE-specific code stays in `InvestView.Infrastructure`.

## Backend Architecture

### InvestView.Api

Responsibilities:

- HTTP request/response mapping.
- JWT authentication and authorization.
- Swagger/OpenAPI.
- SignalR `QuoteHub`.
- Global exception handling and request logging.

Controllers stay thin. They call application services and return DTOs.

### InvestView.Application

Responsibilities:

- Use cases.
- DTOs.
- Validation.
- Service interfaces.
- Transaction boundaries.

Initial use cases:

- `GetMarketBoard`
- `GetSymbolDetail`
- `GetPortfolio`
- `AddWatchlistItem`
- `RemoveWatchlistItem`
- `PlaceSimulatedOrder`
- `CancelSimulatedOrder`
- `GetOrderHistory`

### InvestView.Domain

Responsibilities:

- Domain entities.
- Value objects.
- Enums.
- Trading simulation rules.

Initial domain concepts:

- `User`
- `Symbol`
- `QuoteSnapshot`
- `WatchlistItem`
- `CashAccount`
- `Holding`
- `Order`
- `Execution`
- `PortfolioSnapshot`

Trading simulation rules:

- Buy orders require sufficient simulated cash before acceptance.
- Sell orders require sufficient available holdings before acceptance.
- Orders may be `Pending`, `Open`, `Filled`, `PartiallyFilled`, `Cancelled`, or `Rejected`.
- MVP fills eligible simulated limit orders against the latest quote snapshot using deterministic rules.
- Every order and execution must be clearly labeled as simulated.
- No order flow may call a real brokerage order endpoint.

### InvestView.Infrastructure

Responsibilities:

- EF Core and SQL Server persistence.
- DNSE REST and WebSocket clients.
- Market data provider implementations.
- Cache wrappers.
- Seed/mock market data.

Provider interfaces:

Initial app-owned market data contracts:

- `MarketQuoteDto`: normalized market-board row and snapshot/update payload.
- `PriceLevelDto`: normalized bid/ask level with price and quantity.
- `SymbolDetailDto`: symbol metadata, exchange/board data, and daily reference data.
- `OhlcBarDto`: normalized historical bar data.
- `MarketDataChannel`: app-owned channel enum such as trade, top price, security definition, OHLC, and session.
- `MarketQuoteUpdateDto`: app-owned partial realtime update payload. It can carry matched price, bid/ask levels, foreign trading, trading status, high/low/open, and daily reference/ceiling/floor fields without exposing DNSE payload names to the frontend.

```csharp
public interface IMarketDataProvider
{
    Task<IReadOnlyList<MarketQuoteDto>> GetMarketBoardAsync(
        IReadOnlyCollection<string> symbols,
        string boardId,
        CancellationToken cancellationToken);

    Task<SymbolDetailDto?> GetSymbolDetailAsync(string symbol, CancellationToken cancellationToken);
    Task<IReadOnlyList<OhlcBarDto>> GetOhlcAsync(string symbol, string resolution, CancellationToken cancellationToken);
}

public interface IMarketDataStream
{
    Task SubscribeAsync(
        IReadOnlyCollection<string> symbols,
        string boardId,
        IReadOnlyCollection<MarketDataChannel> channels,
        CancellationToken cancellationToken);
}
```

Implementations:

- `MockMarketDataProvider`: local demo without DNSE credentials.
- `DnseMarketDataProvider`: REST adapter for DNSE market data.
- `CachedMarketDataProvider`: caching decorator around market data provider.
- `DnseMarketDataStream`: inbound DNSE WebSocket client.

## Frontend Architecture

The frontend uses a feature-based structure.

```text
app/
  router, providers, app shell, query client setup

features/market-board/
  market board page, AG Grid configuration, quote mapping, quote update store

features/watchlist/
  watchlist panel, add/remove interactions

features/order-ticket/
  buy/sell order form, client-side validation, submission states

features/portfolio/
  cash, holdings, portfolio value, P/L summary

shared/api/
  typed API client, request helpers, generated/openapi types later

shared/realtime/
  SignalR connection, quote subscription helpers

shared/ui/
  small reusable UI primitives after duplication appears
```

Frontend principles:

- Optimize for dense financial data scanning, not landing-page visuals.
- Follow `docs/design.md` for Tailwind tokens, layout rules, market-board columns, and price color semantics.
- Keep API clients separate from UI components.
- Use TypeScript types for API responses and component props.
- Keep realtime state localized to market/watchlist features.
- Avoid full-page re-renders when a single quote changes.
- Use AG Grid Community for the market board because the UI needs grouped headers, pinned columns, dense cells, virtualization, and future high-frequency quote updates.
- Keep AG Grid as a view-layer implementation detail. API types, quote mapping, price color rules, and SignalR update merge logic must remain InvestView-owned and testable outside the grid where practical.
- Do not use AG Grid Enterprise features for the MVP.

Market board UI requirements:

- Render a securities-style grouped header, not a generic table.
- Include at least: `CK`, `Trần`, `Sàn`, `TC`, 3 bid price/quantity levels, matched price/quantity/change/change percent, 3 ask price/quantity levels, total volume, high, low, status, and updated time.
- Use Vietnamese market color semantics: ceiling purple, floor cyan/blue, reference yellow, increase green, decrease red, unchanged/reference yellow or neutral according to context.
- Keep the symbol column pinned/sticky on wide horizontal boards.
- Support horizontal scroll on narrow screens without breaking row alignment.
- For Task 5, load the initial snapshot from `GET /api/market/quotes`; SignalR updates are added in Task 7.

## DNSE Market Data Integration

DNSE REST APIs provide baseline and historical market data. The market data documentation includes endpoints for instrument details, security definitions, OHLC history, latest trades, latest bid/ask, trading days, foreign investor data, and trading session data.

The DNSE SDK stored under `docs/openapi-sdk-main` is treated as the implementation reference when the public documentation is ambiguous. The SDK clarifies REST signature construction, default API version, configurable date header, WebSocket authentication, channel names, message type mapping, reconnect behavior, and payload model differences.

DNSE-specific protocol code must stay in `InvestView.Infrastructure`. Application, API, SignalR, and React code use InvestView-owned contracts only.

MVP REST usage:

- `GET /instruments`: symbol metadata and basic instrument data.
- `GET /price/:symbol/secdef`: ceiling, floor, reference price, and daily trading status.
- `GET /price/ohlc`: historical OHLC bars for symbol detail chart.
- `GET /price/:symbol/trades/latest`: latest matched trades.
- `GET /price/:symbol/quotes/latest`: latest bid/ask data.
- `GET /market/trading-session`: trading session state where the market board needs exchange/session labels.
- `GET /market/working-dates`: optional calendar support for end-of-day and stale-data handling.

REST adapter requirements:

- Base URL defaults to `https://openapi.dnse.com.vn`.
- API version defaults to `2026-05-07` unless overridden by configuration.
- The date header defaults to `Date`, but the header name must be configurable because DNSE examples may also use `X-Aux-Date`.
- REST signatures must be implemented in an isolated signer service and covered by unit tests with deterministic timestamp and nonce inputs.
- The local SDK signs the request method plus path, date header, and nonce; query parameters are sent on the URL but are not part of the SDK signature string.
- REST response models must be mapped into internal DTOs before leaving Infrastructure.

DNSE WebSocket provides realtime market updates. The documented base stream endpoint is:

```text
wss://ws-openapi.dnse.com.vn/v1/stream?encoding={encoding}
```

MVP WebSocket choices:

- Use `encoding=json` first for debuggability.
- Consider `msgpack` later for performance.
- Authenticate after connection/welcome using backend-held `api_key` and `api_secret`.
- WebSocket authentication uses an HMAC-SHA256 hex digest over `api_key:timestamp:nonce`, which is different from the REST HTTP signature flow.
- Subscribe to channels needed for the market board:
  - `tick.G1.json` for trade ticks.
  - `top_price.G1.json` for best bid/ask.
  - `security_definition.G1.json` where daily reference/ceiling/floor status is needed.
  - `foreign.G1.json` for foreign buy volume, foreign sell volume, and remaining foreign room.
- Optionally subscribe to `session.{productGroupId}.G1.json` when the UI needs session state.
- Handle DNSE application-level `ping` and reply with `pong`.
- Reconnect with backoff and resubscribe after disconnect.
- Treat control messages by known `action` values, and market data messages by payload type such as `T`.
- Preserve per-symbol update ordering inside the backend stream adapter before broadcasting through SignalR.
- Expose a health signal for DNSE stream status: connected, authenticated, last pong, subscriptions, and last message time.

The backend converts DNSE-specific payloads into InvestView DTOs before sending them to the frontend.
The first implemented DNSE WebSocket source is `DnseWebSocketQuoteStreamService`, selected with `MarketData:QuoteStream:SourceProvider = "DnseWebSocket"`. Mock realtime remains the safe default.

Known DNSE mapping details from the local SDK:

- WebSocket message types include `t` for trade, `te` for trade extra, `sd` for security definition, `q` for top price, `b` for OHLC, `bc` for closed OHLC, and `s` for trading session.
- REST quote price levels use `quantity`; WebSocket quote models use `qtty`. Internal DTOs must normalize both to one field name.
- DNSE timestamps may arrive as ISO strings, Unix timestamps, or protobuf-style objects with seconds/nanos. Internal DTOs should use a single timestamp type.
- `G1` is the first MVP board scope for normal stock market-board data unless a later spec update expands the market scope.

Do not copy unsafe SDK behavior blindly. In particular, production .NET HTTP clients must keep TLS certificate validation enabled.

## Caching Strategy

Caching is part of the MVP because the FSS job description calls for REST API and caching understanding.

Use `IMemoryCache` first:

- Symbol metadata from `/instruments`: 30-60 minutes.
- Security definition from `/price/:symbol/secdef`: until the end of the trading day or 30 minutes in demo mode.
- OHLC history from `/price/ohlc`: 5-15 minutes.
- Latest quote/trade snapshots: 1-3 seconds.
- Latest WebSocket snapshot per symbol: in-memory state updated by `DnseMarketDataStream`.

Cache behavior must be visible enough to discuss in interview:

- Cache keys are named consistently.
- TTLs are explicit.
- Logs include cache hit/miss for market data reads in development.
- Tests cover the caching decorator at the application/infrastructure boundary.

Redis is out of MVP unless explicitly approved later.

## Realtime Strategy

Realtime has two separate boundaries:

```text
DNSE WebSocket -> InvestView backend -> SignalR -> React frontend
```

The backend owns DNSE connectivity. SignalR is the app-facing realtime contract.

Reasons:

- Protect DNSE credentials.
- Normalize DNSE payloads before they reach the UI.
- Keep frontend independent from DNSE protocol details.
- Allow mock stream fallback when credentials are unavailable.
- Demonstrate server-side realtime coordination.

## REST API Surface

MVP API endpoints:

```text
POST   /api/auth/login

GET    /api/market/symbols
GET    /api/market/quotes
GET    /api/market/symbols/{symbol}
GET    /api/market/symbols/{symbol}/ohlc

GET    /api/watchlist
POST   /api/watchlist/{symbol}
DELETE /api/watchlist/{symbol}

GET    /api/portfolio

GET    /api/orders
POST   /api/orders
POST   /api/orders/{orderId}/cancel
```

Realtime endpoint:

```text
/hubs/quotes
```

OpenAPI/Swagger must document the REST surface. SignalR contracts should be documented in `docs/architecture` when implemented.

## Database Model

Initial tables:

- `Users`
- `Symbols`
- `WatchlistItems`
- `CashAccounts`
- `Holdings`
- `Orders`
- `Executions`
- `MarketQuoteSnapshots`
- `PortfolioSnapshots`

Persistence rules:

- Store simulated trading state in SQL Server.
- Store durable user/watchlist/order/portfolio state.
- Store selected market snapshots only when they support demo, audit, or portfolio valuation.
- Do not persist DNSE API secrets in plaintext.
- Do not persist raw third-party payloads unless needed for debugging and explicitly approved.

## Code Style

Backend style target:

```csharp
public sealed class PlaceOrderRequest
{
    public required string Symbol { get; init; }
    public required OrderSide Side { get; init; }
    public required OrderType Type { get; init; }
    public required decimal Price { get; init; }
    public required int Quantity { get; init; }
}
```

Backend rules:

- Keep domain rules out of controllers.
- Prefer explicit DTOs over exposing EF entities.
- Validate at API boundaries and inside domain/application rules where invariants matter.
- Use clear securities names: `Order`, `Execution`, `Holding`, `Quote`, `Watchlist`, `Portfolio`.
- Keep DNSE-specific names inside infrastructure mapping code.

Frontend style target:

```tsx
type MarketQuoteRowProps = {
  symbol: string;
  lastPrice: number;
  changePercent: number;
  totalVolume: number;
  tradingStatus: string;
};
```

Frontend rules:

- Use TypeScript types for API responses and component props.
- Keep API and SignalR clients outside UI components.
- Build reusable UI primitives only after repeated patterns appear.
- Avoid marketing-style layout. The product should feel like an operational securities tool.

## Testing Strategy

Backend:

- Unit test domain rules:
  - buy order cash validation,
  - sell order holdings validation,
  - order status transitions,
  - execution creation,
  - portfolio value calculation.
- Unit test market data adapters with mocked DNSE HTTP/WebSocket responses.
- Unit test `CachedMarketDataProvider` TTL/hit/miss behavior.
- Integration test REST flows:
  - login,
  - get market board,
  - add/remove watchlist item,
  - place order,
  - cancel order,
  - get portfolio.
- Integration test EF Core mappings and important constraints after schema exists.

Frontend:

- Component tests for:
  - market board rendering,
  - market board price color rules and quote mapping,
  - watchlist add/remove,
  - order ticket validation,
  - portfolio summary.
- API client tests with mocked backend responses.
- Realtime tests for quote update handling where practical.
- Manual browser verification after each vertical slice.

Minimum MVP verification:

```powershell
dotnet build
dotnet test
npm run build
npm run test
docker compose up --build
```

Manual demo verification:

1. Sign in with demo account.
2. View market board.
3. See quote updates through SignalR.
4. Add a symbol to watchlist.
5. Place a simulated buy order.
6. See cash, holdings, and portfolio value update.
7. Cancel an open simulated order.

## Boundaries

Always do:

- Keep simulated trading separate from real brokerage order submission.
- Keep DNSE credentials on the backend and out of source control.
- Support mock market data for local demo without credentials.
- Add tests for domain rules affecting cash, holdings, orders, executions, or caching.
- Keep frontend workflows responsive on desktop and mobile widths.
- Update this spec before changing scope.

Ask first:

- Adding Redis, background jobs, Kubernetes manifests, or message queues.
- Adding paid providers or SDKs.
- Persisting raw DNSE payloads.
- Changing away from ASP.NET Core, React, or SQL Server.
- Implementing real order submission.
- Expanding MVP to admin/back-office workflows.

Never do:

- Submit real securities orders.
- Store real brokerage credentials in plaintext.
- Expose DNSE `api_key` or `api_secret` to the frontend.
- Present simulated order performance as real investment performance.
- Clone FSS, SSI, DNSE, or any brokerage's proprietary UI exactly.
- Hide mock data behind labels that imply it is live real-market data.

## Success Criteria

MVP is complete when:

- The app runs locally with documented commands.
- A demo user can sign in.
- The market board displays stock symbols with quote-like data.
- DNSE-backed provider can be enabled with environment variables.
- Mock market data works without DNSE credentials.
- Quote updates reach the React UI through SignalR.
- A user can manage a watchlist.
- A user can place and cancel simulated orders.
- Filled simulated orders update cash, holdings, and portfolio value.
- Core REST APIs are documented with Swagger.
- SQL Server schema supports the MVP flows.
- Core domain rules and caching behavior have automated tests.
- Docker Compose can run the local demo stack.

## Out of Scope for MVP

- Real money trading.
- Real order routing.
- Full brokerage account integration.
- Full KYC/eKYC.
- Deposits and withdrawals.
- Admin/back-office.
- Margin, derivatives, covered warrants, bonds, or funds.
- Complex order book matching.
- Redis, Kubernetes, Docker Swarm production deployment.
- Exact clone of any real securities platform UI.

## Open Questions

1. Will DNSE credentials be available during development, or should the first implementation rely on mock data only?
2. Should demo auth use a seeded local user only, or allow simple registration?
3. Should the first market scope focus on Vietnamese stock symbols only?
4. Should the first chart use OHLC candles, a simple line chart, or no chart until core trading flow is complete?
