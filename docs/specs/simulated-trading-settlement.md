# Spec: Simulated trading settlement

## Objective

Make simulated trading behave closer to the Vietnamese equity market while staying simple enough for a demo account.

The system still simulates execution and does not model scarce liquidity: when an order satisfies the current market condition, it is filled in full. After a sell fills, cash is available immediately. After a buy fills, the stock is owned but remains in a pending-receive state until settlement makes it available to sell.

## Current scope

Implement the first settlement slice for LO and MTL orders:

- Order placement is rejected when the current stock market session is not open.
- For this slice, only continuous-session LO/MTL trading is supported.
- MTL orders always use the latest available market price and fill in full.
- LO buy fills in full when limit price is greater than or equal to the market price.
- LO sell fills in full when limit price is less than or equal to the market price.
- Non-marketable LO buy remains pending and reserves cash.
- Non-marketable LO sell remains pending and reserves available stock.
- Filled buy decreases cash immediately and increases total holding quantity, but not sellable quantity.
- Filled sell decreases sellable holding quantity and credits cash immediately.
- ATO/ATC matching remains out of scope for this slice.

## Portfolio quantities

Holdings must expose enough information for the "Danh mục nắm giữ" UI:

- `quantity`: total owned shares, including shares waiting to settle.
- `availableQuantity`: shares that can be sold now.
- `pendingReceiveQuantity`: bought shares waiting to become available.
- reserved sell quantity can be inferred as `quantity - availableQuantity - pendingReceiveQuantity`.

## Tasks

1. Add domain support for pending-receive holdings.
2. Update order placement so filled buy orders add stock to pending receive instead of available quantity.
3. Expose pending receive quantity through portfolio DTO/API/frontend types.
4. Update tests around buy fill, insufficient sellable quantity, pending sell reservation, and portfolio response.
5. Keep ATO/ATC matching and automatic end-of-day settlement for a later slice.

## Success criteria

- Buying 100 shares with a marketable order creates a filled order, debits cash, returns portfolio `quantity = 100`, `availableQuantity = 0`, and `pendingReceiveQuantity = 100`.
- Selling right after that buy is rejected because the shares are not available yet.
- Selling an already available holding still fills in full and credits cash immediately.
- Pending LO buy and pending LO sell cancellation still release reserved cash/stock.
- Placing an order while the resolved market session is closed, pre-open, or lunch break returns a bad request and does not create an order.
