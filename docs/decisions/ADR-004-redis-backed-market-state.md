# ADR-004: Use Redis-Backed Market State With Pub/Sub Broadcast Pipeline

## Status
Accepted

## Date
2026-07-09

## Context
InvestView now integrates DNSE REST snapshots and DNSE WebSocket realtime updates. DNSE has rate limits, and a market board can include tens or hundreds of symbols. Refreshing a board snapshot from REST during the trading session would fan out into many upstream calls and quickly waste rate limit budget.

The correct freshness source during the trading session is the DNSE WebSocket stream. REST should create a baseline snapshot when state is missing, after restart, or as fallback/backfill. The backend also needs to support future production deployment with multiple API servers. In that topology, the server that receives DNSE WebSocket messages may not be the server that owns every connected SignalR client.

## Decision
Use a Redis-backed latest-state pipeline with local in-memory mirrors on each API server.

The selected flow is option B: all client broadcasts go through an internal Redis Pub/Sub event path, including the API server that originally received the DNSE WebSocket update.

```text
DNSE WebSocket
  -> feed handler / mapper
  -> merge into latest market state
  -> write Redis latest-state
  -> publish Redis Pub/Sub market event
  -> each API server subscriber updates local memory mirror
  -> each API server broadcasts SignalR to its connected clients
```

REST snapshot APIs read local memory first, Redis second, and call DNSE REST only when the requested state is missing and must be backfilled. REST is a baseline/backfill mechanism, not the normal freshness mechanism during the session.

Redis keys are organized by canonical market-data subject rather than by UI screen. A quote is stored once per `boardId:symbol`, while market boards, categories, and watchlists resolve to symbol membership lists that point to those quote aggregates. Quote, symbol detail, and index state use Redis Hashes with a canonical `payload` field plus materialized scalar fields for inspection and future field-level update optimization. Latest trades use Redis Lists. OHLC ranges use Redis Sorted Sets with coverage tokens so the API does not accidentally return a partial historical range as a full cache hit.

Market-board refreshes that do not include explicit symbols must resolve Redis membership first: category/index membership, then market membership, then board membership. REST fallback is scoped to missing symbols only; it must not re-fetch the entire board when Redis already contains the board membership and most quote aggregates.

EOD persistence is explicitly out of scope for this decision. Redis is the realtime/latest-state source. A durable database or event store can be added later for historical/audit data.

## Alternatives Considered

### Broadcast directly from DNSE WebSocket handler
- Pros: Lowest latency on a single API server; simplest implementation.
- Cons: Does not deliver updates to clients connected to other API servers unless SignalR backplane or another fan-out path exists. Creates two processing paths once Redis is added.
- Rejected: The project is moving beyond MVP and should not bake in a single-server assumption.

### Broadcast locally and also publish Redis Pub/Sub
- Pros: Clients connected to the feed-owning server receive the update slightly sooner.
- Cons: The origin server must avoid double-processing its own Pub/Sub event. Broadcast logic is split across direct and subscriber paths.
- Rejected: The latency win is small compared with the operational simplicity of one path.

### Use REST polling with short TTL during trading session
- Pros: Easy to reason about as request/response caching.
- Cons: Burns DNSE rate limit because each board snapshot can require many upstream calls. Also duplicates WebSocket freshness work.
- Rejected: REST polling is the wrong freshness mechanism for realtime market boards.

### Use Redis Streams immediately
- Pros: Supports replay and consumer groups; better foundation for EOD/audit pipelines.
- Cons: Larger implementation scope and not required until historical persistence or replay is needed.
- Deferred: Redis Streams can be added when EOD persistence or reliable replay becomes a current requirement.

## Consequences
- The backend uses app-owned abstractions for latest market state, local mirror reads, and internal market event publishing/subscription.
- WebSocket update merge logic must be careful with partial payloads: missing fields must not overwrite existing values.
- Every market update should carry an `updatedAt` and, if available later, sequence information to reject stale updates.
- Local memory is a hot mirror, not the shared source. Redis is the shared latest-state layer.
- Frontend contracts remain stable: clients still receive REST snapshots and SignalR deltas.
- Redis is required for the runtime shared latest-state and Pub/Sub path. In-memory state remains only as each API server's local hot mirror and as test infrastructure, not as a production fallback.
- Runtime market data must not use a separate process-local memory cache. Any shared market snapshot/cache data should be stored in Redis so all API servers observe the same latest state.
- REST-backed symbol detail and OHLC reads also pass through Redis. DNSE REST is used only to backfill missing Redis state, then subsequent reads are served through the same Redis/local mirror path.
- Category/market/board refreshes use Redis symbol membership and partial REST backfill to preserve DNSE rate limit budget.
