# Kế hoạch triển khai: Trading drawer

## Architecture Decisions

- Keep selection and open state in `MarketBoard`, where live quote data already exists.
- Compose existing trading features inside a controlled drawer rather than moving their business logic.
- Move technical market-data status to the existing footer.
- Keep the reference interface code-native in React/Tailwind and adopt project-owned shadcn/ui components on Radix primitives.
- Use the current quote, portfolio, and order APIs as the data source for the richer drawer.

## Task List

### Task 1: Lock drawer behavior with an integration test

- Acceptance: toolbar CTA opens the drawer; close action hides it; no inline ticket exists while closed.
- Verification: focused MarketBoard test follows RED then GREEN.
- Files: `MarketBoard.test.tsx`.

### Task 2: Add the trading drawer and recompose the workspace

- Acceptance: drawer renders order ticket and portfolio; App removes permanent portfolio; MarketBoard removes inline ticket.
- Verification: focused integration and component tests pass.
- Files: `TradingDrawer.tsx`, `MarketBoard.tsx`, `App.tsx`.
- Dependency: Task 1.

### Task 3: Relocate technical status and verify

- Acceptance: toolbar contains `Đặt lệnh` instead of status badges; footer retains connection/snapshot context.
- Verification: tests, lint, and build pass.
- Dependency: Task 2.

### Task 4: Lock the reference layout with component tests

- Acceptance: tests cover the primary tabs, quote/order fields, two side actions, ledger tabs, and empty ledger.
- Verification: `TradingDrawer.test.tsx` and `OrderTicketPanel.test.tsx` fail before implementation and pass afterward.
- Files: `TradingDrawer.test.tsx`, `OrderTicketPanel.test.tsx`.

### Task 5: Rebuild the drawer chrome and spot ticket

- Acceptance: the drawer is 448 px wide and matches the reference hierarchy, spacing, colors, and controls while preserving simulated-order validation and submission.
- Verification: focused component tests and production build pass.
- Files: `TradingDrawer.tsx`, `OrderTicketPanel.tsx`.
- Dependency: Task 4.

### Task 6: Add real-data ledger and asset tabs

- Acceptance: `Sổ lệnh` renders demo orders or the reference empty state; `Tài sản` composes the existing portfolio panel; unsupported tabs are honest placeholders.
- Verification: focused tests, complete frontend tests, lint, and build.
- Files: `OrderTicketPanel.tsx`, related tests.
- Dependency: Task 5.

### Task 7: Establish the shadcn/ui foundation

- Acceptance: Vite/Tailwind aliases and semantic theme tokens are configured; only the primitives required by trading are added.
- Verification: TypeScript build and focused component tests pass.
- Files: `package.json`, lockfile, TypeScript/Vite config, `components.json`, `src/components/ui/*`, `src/lib/utils.ts`, `src/index.css`.

### Task 8: Migrate the trading panel to shadcn/ui

- Acceptance: Sheet, primary/lower tabs, select, switch, checkbox, inputs, buttons, and tooltips use shadcn/ui while order submission and portfolio behavior remain unchanged.
- Verification: component tests follow RED/GREEN; MarketBoard drawer integration, lint, build, and dependency audit pass.
- Files: trading and order-ticket components/tests.
- Dependency: Task 7.

### Task 9: Correct drawer capacity and numeric steppers

- Acceptance: drawer is responsive up to 560 px, derivatives are absent, and quantity/price use shadcn `InputGroup` with embedded step buttons.
- Verification: focused trading tests, integration test, lint, and build pass.
- Files: `TradingDrawer.tsx`, `OrderTicketControls.tsx`, related tests, `components/ui/input-group.tsx`.
- Dependency: Task 8.

## Risks and Mitigations

- The reference is desktop-only: use full width below 448 px and retain internal scrolling for short viewports.
- The MarketBoard suite currently has unrelated label assertions failing in dirty files: run the new test by name and report the pre-existing full-suite blockers separately.
- Overlay can trap page scroll: restore body overflow in effect cleanup.
