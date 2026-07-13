# Spec: Trading drawer

## Objective

Remove persistent simulated-account and order-entry strips from the market workspace. Keep the market-board toolbar focused on discovery and filtering, expose a single `Đặt lệnh` call to action, and open simulated trading in an accessible right-side drawer that closely matches the supplied 448 px reference design.

The drawer combines the reference layout with the application's real demo data:

- quote header values come from the selected live quote;
- account buying power, orders, and assets come from the authenticated demo portfolio;
- order submission continues to use the existing simulated-order API;
- derivatives and conditional-order features remain clearly marked placeholders until their backend exists.

## Tech Stack

- React 19 and TypeScript
- Tailwind CSS utilities already used by the application
- shadcn/ui source components on Radix UI primitives
- Existing `OrderTicketPanel`, `PortfolioPanel`, and demo-session behavior
- Vitest and Testing Library

## Commands

Run from `src/investview.web`:

- Focused test: `npm test -- --run src/features/market-board/MarketBoard.test.tsx -t "trading drawer"`
- Frontend tests: `npm test`
- Lint: `npm run lint`
- Build: `npm run build`

## Project Structure

- `features/trading/TradingDrawer.tsx` owns the overlay, dialog accessibility, and the three primary trading tabs.
- `components/ui/` contains the project-owned shadcn/ui primitives used by the drawer.
- `features/order-ticket/OrderTicketPanel.tsx` owns the spot quote header, order form, and lower account tabs.
- `features/order-ticket/OrderTicketControls.tsx` owns the compact quote and numeric controls.
- `features/order-ticket/OrderAccountArea.tsx` owns the order ledger, holdings, and asset tabs.
- `features/portfolio/PortfolioPanel.tsx` remains the existing real-data asset view used by the `Tài sản` tab.
- `features/market-board/MarketBoard.tsx` owns the selected symbol and drawer open state.
- `app/App.tsx` no longer renders a persistent portfolio strip.

## Code Style

Use a controlled drawer contract:

```ts
type TradingDrawerProps = {
  isOpen: boolean;
  onClose: () => void;
  liveQuote: MarketQuote | null;
  selection: SymbolDetailSelection | null;
};
```

The drawer owns presentation only; order and portfolio business logic remain in their existing feature hooks and API modules. shadcn/ui components provide interaction and accessibility behavior, while feature components retain business state and market-specific styling.

The shadcn CLI is invoked through `npx` for scaffolding and is not shipped as an application dependency. Required Radix state variants live in `index.css`, and generated components remain project-owned source under `components/ui`.

## Testing Strategy

- Integration-test the market-board toolbar button opening and closing the drawer.
- Component-test the primary tabs, reference form labels, buy/sell actions, and lower ledger tabs.
- Verify the order ticket is absent while the drawer is closed.
- Preserve existing order-ticket, portfolio, market-board, and authentication behavior.
- Build and lint after the layout change.

## Boundaries

- Always: preserve selected-symbol state; support Escape, backdrop, and explicit close; keep technical connection state visible outside the main toolbar; disable order submission without a valid login, symbol, and quantity.
- Ask first: change order matching, add backend contracts, or introduce a second competing UI system.
- Never: duplicate order or portfolio business logic in the drawer; remove realtime status entirely; overwrite unrelated market-index or symbol-detail work.

## Success Criteria

- `PortfolioPanel` is not rendered as a permanent row in `App`.
- `OrderTicketPanel` is not rendered inline below the grid.
- The toolbar no longer contains row count, REST badge, or realtime badge.
- A right-aligned `Đặt lệnh` button opens a responsive modal right drawer up to 560 px wide so no form control or ledger column is clipped.
- The top row contains `Giao dịch cơ sở`, `Đặt lệnh điều kiện`, and a close action; derivatives are outside this demo scope and are not rendered.
- The spot view contains selected-symbol quote data, account, buying power, automatic-price toggle, quantity, price, MTL/ATO/ATC controls, authentication controls, and separate `Mua`/`Bán` actions.
- The lower area contains `Sổ lệnh`, `Sổ lệnh điều kiện`, `Danh mục`, and `Tài sản`; the first and last tabs use real demo data.
- Empty orders render the reference table header and `Không tìm thấy lệnh nào` state.
- The drawer can be closed by its close action, backdrop, or Escape.
- Drawer, tabs, select, switch, checkbox, inputs, buttons, and tooltips use project-owned shadcn/ui primitives rather than feature-local interaction implementations.
- AG Grid and Lightweight Charts remain unchanged.
- Snapshot/realtime details appear in the compact footer.
- Focused tests, lint, and production build pass.

## Open Questions

- Below 560 px viewport width, the drawer uses the full available viewport width.
- MTL, ATO, and ATC share the current market-order simulation because the API only distinguishes market and limit prices.
