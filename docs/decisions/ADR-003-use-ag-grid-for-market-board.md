# ADR-003: Use AG Grid Community for Market Board UI

## Status

Accepted

## Date

2026-07-06

## Context

InvestView needs a dense Vietnamese securities-style market board, not a simple static table. The board must display grouped columns for reference prices, bid/ask depth, matched price, volume, high/low, and trading status. Later milestones will add SignalR quote updates, so the UI must update rows and cells without re-rendering the whole page on every tick.

The FSS job description emphasizes ReactJS, JavaScript ES6+, HTML5/CSS3, RESTful API integration, caching, UI/UX performance, and realtime data handling for securities trading systems. The market board is the strongest frontend slice for demonstrating those requirements.

AG Grid's React grid supports grouped columns, pinned columns, custom cell rendering/class rules, row identity, virtualization, and transaction-based updates. Its high-frequency update API can batch continuous updates, which fits the planned REST snapshot plus SignalR update flow.

## Decision

Use AG Grid Community with React and TypeScript for the market board.

For Task 5, AG Grid is used for the REST snapshot market board only:

- grouped headers matching a real price board shape;
- pinned symbol column;
- 3 bid levels and 3 ask levels;
- reference, ceiling, floor, last, change, change percent, volume, high, low, and status fields;
- securities color rules for ceiling, floor, reference, increase, decrease, and unchanged prices;
- loading, error, and empty states.

For Task 7, keep the same grid and apply SignalR quote updates through row identity plus grid transactions instead of replacing all row data on every tick.

AG Grid is an implementation detail of the frontend. Backend contracts, REST APIs, SignalR payloads, and application DTOs remain InvestView-owned and provider-neutral.

## Alternatives Considered

### Native HTML table

- Pros: simple, semantic, easy to test, no dependency.
- Cons: more custom work for grouped headers, pinned columns, virtualization, and frequent row/cell updates.
- Rejected because the market board is a high-density realtime data grid, not a low-volume report table.

### TanStack Table plus TanStack Virtual

- Pros: headless, flexible, strong React fit, avoids a heavy grid abstraction.
- Cons: requires more custom implementation for grid behaviors that market boards naturally need: pinned columns, column sizing, dense keyboard/navigation behavior, row update batching, and polished grouped headers.
- Rejected for MVP because it increases frontend infrastructure work before the core portfolio workflow is proven.

### Canvas-based grid such as Glide Data Grid

- Pros: very fast for huge datasets and smooth scrolling.
- Cons: harder to customize with ordinary React components, harder to test with DOM-based React Testing Library, and more complex for accessibility and styling.
- Rejected for MVP because the first board is wide and realtime-oriented, but not large enough to justify a canvas-first approach.

### AG Grid Enterprise

- Pros: more advanced enterprise grid features.
- Cons: paid features are unnecessary for the MVP and add licensing concerns.
- Rejected; use AG Grid Community only.

## Consequences

- Frontend gains a grid engine suited to dense financial data and future realtime updates.
- Task 5 must include AG Grid dependencies and isolate grid configuration from API/data-fetching code.
- Tests should cover mapping, rendering states, and securities color rules. Runtime grid behavior that is difficult in jsdom can be verified through focused helper tests plus manual browser checks.
- The project remains aligned with the FSS JD because the interview story centers on React, REST integration, realtime update strategy, UI performance, and securities domain modeling; AG Grid is the tool used to implement the grid efficiently.
- The team must avoid coupling business rules to AG Grid. Quote DTOs, color rules, and update merge logic should remain testable outside the grid component where practical.
