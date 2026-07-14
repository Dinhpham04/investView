# Spec: Simulated trading settlement

## Objective

Make simulated trading track when bought shares become sellable. The simulator still assumes infinite market liquidity: any order that satisfies the current market matching rule is filled in full at the simulated execution price. Settlement starts after the order is filled.

Users must be able to answer:

- How many shares do I own?
- How many shares can I sell now?
- How many bought shares are waiting for settlement?
- Which pending shares are T0/T1/T2?
- What is the next date a pending share lot becomes sellable?

## Product rules

- The simulator does not model scarce liquidity, visible order-book depth, queue priority, or partial fills.
- MTL buy/sell fills in full when the market is open and a valid market price exists.
- LO buy fills in full when `limitPrice >= marketPrice`; otherwise it remains pending and reserves cash.
- LO sell fills in full when `limitPrice <= marketPrice`; otherwise it remains pending and reserves sellable shares.
- Filled sell credits cash immediately.
- Filled buy debits cash immediately, increases total holding quantity, and creates pending settlement lots.
- Bought shares are not sellable until their settlement lot becomes available.
- `Holding.PendingReceiveQuantity` is a cached summary. Settlement lots are the source of truth for when shares become available.

## Production data model

### Holding

Aggregated current state for a user/symbol/board.

- `Quantity`: total owned shares, including shares waiting for settlement.
- `AvailableQuantity`: shares that can be sold now.
- `PendingReceiveQuantity`: bought shares waiting to become sellable.
- `AverageCost`: weighted average cost.

### HoldingSettlementLot

One filled buy execution creates one pending settlement lot.

- `UserId`
- `Symbol`
- `BoardId`
- `SourceOrderId`
- `SourceExecutionId`
- `Quantity`
- `RemainingQuantity`
- `TradeDate`
- `SettlementDate`
- `AvailableFromDate`
- `Status`: `Pending`, `Settled`, `Cancelled`, `Failed`
- `CreatedAt`
- `SettledAt`

### Trading calendar

Settlement dates must be calculated using trading days, not calendar days. The first implementation may generate weekday trading days in code, but the abstraction must allow a holiday-aware calendar later.

## Task list

### Phase 1: Settlement foundation

1. Add `HoldingSettlementLot` domain entity and EF persistence.
2. Add trading-day calendar abstraction and settlement-date calculator.
3. Create a pending settlement lot whenever a buy order fills in full.
4. Add tests proving filled buys create pending lots with a real `AvailableFromDate`.

### Phase 2: Settlement processing

5. Add settlement processor that settles due lots idempotently.
6. Add settlement run/audit records for observability.
7. Add tests for due lots, not-yet-due lots, repeat runs, and failure handling.

### Phase 3: Holdings API

8. Add `GET /api/portfolio/holdings`.
9. Aggregate holdings with pending T0/T1/T2 quantities and `nextAvailableDate`.
10. Add API tests for empty, available-only, pending-only, and mixed portfolios.

### Phase 4: Holdings UI

11. Add “Danh mục nắm giữ” as a navbar view next to “Bảng giá”.
12. Render holdings with AG Grid, grouped pending columns, footer totals, and sell action.
13. Add lot detail drilldown for pending T0/T1/T2 quantities.

## Success criteria

- A marketable buy for 100 shares fills in full and creates exactly one pending settlement lot for 100 shares.
- The holding immediately has `Quantity = 100`, `AvailableQuantity = 0`, and `PendingReceiveQuantity = 100`.
- The settlement lot has deterministic `TradeDate`, `SettlementDate`, and `AvailableFromDate`.
- The user cannot sell pending shares before settlement.
- When settlement is processed on or after `AvailableFromDate`, pending quantity moves to available quantity exactly once.
- Holdings API can report T0/T1/T2 pending quantities from settlement lots, not from frontend guesses.

