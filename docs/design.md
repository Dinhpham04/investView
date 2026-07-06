# Design Standard: InvestView Market Workstation

## Status

Accepted working design guide.

Last updated: 2026-07-06.

## Purpose

This document is the UI/UX source of truth for the InvestView frontend before implementing the market board. The visual target is a Vietnamese securities trading workstation inspired by SSI iBoard and other real market boards: dense, dark, numeric, fast to scan, and optimized for realtime quote changes.

This is a functional and domain-style reference, not a pixel-perfect clone. Do not copy SSI logos, proprietary icons, exact spacing, exact wording outside common market terms, or brand-specific visual assets.

## Product Feel

InvestView should feel like an operational securities tool:

- dense but readable;
- dark by default;
- optimized for scanning many numeric cells;
- precise, restrained, and low-decoration;
- built around the market board as the primary first screen;
- clearly labeled as demo/simulated where trading or portfolio actions appear.

Avoid marketing-page composition, oversized hero text, decorative cards, gradient backgrounds, or educational copy inside the product surface.

## Technology Choices

- React with TypeScript for UI composition.
- Tailwind CSS for layout, spacing, typography, and design tokens.
- AG Grid Community for the market board grid engine.
- CSS variables may back Tailwind tokens when AG Grid theming requires non-class-based integration.
- Lucide icons may be used for toolbar actions where an icon is more recognizable than text.

## Layout Model

### App Shell

The main screen is a full-height workstation.

```text
Top bar:     product name, market status, connection state, account/demo badge
Board tabs:  VN30, HOSE, HNX, UPCOM, Watchlist, ETF, CW
Toolbar:     symbol search, board filters, refresh, settings, order button
Main area:   AG Grid market board
Side area:   optional watchlist/order/portfolio panel in later tasks
Status bar:  data source, updated time, REST/realtime state, warning messages
```

Rules:

- Market board gets the largest area.
- Toolbar height stays compact.
- No nested UI cards.
- Page sections are full-width bands or unframed layouts.
- Repeated data items may use compact rows or panels, not decorative cards.

### Desktop Layout

- Minimum comfortable width: `1280px`.
- Market board should fill available width and height.
- Symbol column stays pinned.
- Header and board tabs stay visible.
- Right panel may appear when portfolio/order ticket is implemented.

### Tablet and Mobile Layout

- The board remains horizontally scrollable.
- Do not collapse numeric columns into stacked cards.
- Keep symbol, last price, change, and volume easy to reach.
- Hide optional panels behind tabs or drawers when needed.
- Touch targets outside the grid should be at least `36px` high.

## Tailwind Design Tokens

Use semantic token names instead of raw colors in components. Exact Tailwind configuration can be added in the frontend task, but these tokens define the intended system.

### Surfaces

| Token | Hex | Usage |
|---|---:|---|
| `market-bg` | `#0B0F14` | App background |
| `market-surface` | `#101820` | Header, toolbar, status bar |
| `market-surface-2` | `#16202A` | Grid header, active tab |
| `market-surface-3` | `#1D2935` | Hover, selected row, raised controls |
| `market-border` | `#2A3745` | Grid lines, separators |
| `market-border-strong` | `#3B4B5C` | Active separators, focused controls |

### Text

| Token | Hex | Usage |
|---|---:|---|
| `market-text` | `#E6EDF3` | Primary text |
| `market-text-muted` | `#9AA8B7` | Secondary text |
| `market-text-subtle` | `#687789` | Disabled/placeholder text |
| `market-inverse` | `#05070A` | Text on bright price backgrounds if needed |

### Price Semantics

| Token | Hex | Meaning |
|---|---:|---|
| `price-up` | `#22C55E` | Price above reference |
| `price-down` | `#EF4444` | Price below reference |
| `price-ref` | `#FACC15` | Reference/unchanged price |
| `price-ceiling` | `#D946EF` | Ceiling price |
| `price-floor` | `#22D3EE` | Floor price |
| `price-neutral` | `#CBD5E1` | Missing, paused, or non-comparable value |
| `price-flash-up-bg` | `#12351F` | Temporary background after upward realtime update |
| `price-flash-down-bg` | `#3A1416` | Temporary background after downward realtime update |

### Functional Colors

| Token | Hex | Usage |
|---|---:|---|
| `state-online` | `#2DD4BF` | Connected/realtime healthy |
| `state-warning` | `#F59E0B` | Delayed/stale data |
| `state-error` | `#FB7185` | API/realtime error |
| `action-primary` | `#16A34A` | Buy/order primary action |
| `action-danger` | `#DC2626` | Sell/cancel/destructive action |
| `focus-ring` | `#38BDF8` | Keyboard focus outline |

## Typography

Use compact text. Market boards are read by scanning columns, not reading paragraphs.

| Use | Size | Weight | Notes |
|---|---:|---:|---|
| App title | `16px` | `700` | Header only |
| Section/tab text | `13px` | `600` | Compact uppercase optional |
| Grid header | `12px` | `600` | Centered for grouped headers |
| Grid cell | `12px` | `500` | Numeric cells right-aligned |
| Symbol cell | `13px` | `700` | Left-aligned, uppercase |
| Metadata/status | `11px` | `500` | Muted |

Rules:

- Do not scale font size with viewport width.
- Letter spacing should stay `0`.
- Use tabular numbers where possible: `font-variant-numeric: tabular-nums`.
- Numeric columns are right-aligned.
- Symbol and name columns are left-aligned.

## Spacing and Shape

| Token | Value | Usage |
|---|---:|---|
| `space-1` | `4px` | Cell padding micro spacing |
| `space-2` | `8px` | Toolbar gaps |
| `space-3` | `12px` | Panel padding |
| `space-4` | `16px` | Page horizontal padding |
| `row-height-compact` | `28px` | Market board rows |
| `header-height` | `44px` | Top bar |
| `toolbar-height` | `40px` | Board controls |

Rules:

- Border radius defaults to `6px` or less.
- Market board cells should use square or near-square edges.
- Avoid pill-heavy UI except for compact status badges.
- Do not put cards inside cards.

## Market Board Columns

Task 5 must render a market board close to real Vietnamese quote boards.

Required groups:

```text
CK
Trần | Sàn | TC
Bên mua: Giá 3 | KL 3 | Giá 2 | KL 2 | Giá 1 | KL 1
Khớp lệnh: Giá | KL | +/- | +/- (%)
Bên bán: Giá 1 | KL 1 | Giá 2 | KL 2 | Giá 3 | KL 3
Tổng KL | Cao | Thấp | Trạng thái | Cập nhật
```

Future optional groups:

```text
ĐTNN: NN mua | NN bán | Room
GDNN / thỏa thuận
Watchlist flags
Order shortcut
```

Column behavior:

- `CK` is pinned left.
- Price and quantity cells are fixed width.
- Group headers are center-aligned.
- Data rows use thin grid lines.
- Empty values render as `-`, not `0`.
- Quantity may be shortened only if the label states the unit. For MVP, use raw integer formatting with thousands separators.

## Price Color Rules

Classification order matters:

1. If value is missing, use `price-neutral`.
2. If value equals ceiling price, use `price-ceiling`.
3. If value equals floor price, use `price-floor`.
4. If value equals reference price, use `price-ref`.
5. If value is greater than reference price, use `price-up`.
6. If value is lower than reference price, use `price-down`.
7. Otherwise use `price-neutral`.

Apply the same classification to bid, ask, last, high, and low prices. Change and change percent use `price-up`, `price-down`, or `price-ref` based on sign.

Do not hardcode these rules inside JSX. Implement them as testable helper functions before wiring them into AG Grid cell classes.

## Realtime Interaction Rules

Task 5 is REST snapshot only. It must not label the board as live realtime.

When Task 7 adds SignalR:

- initial rows come from REST;
- updates merge by `symbol`;
- AG Grid row identity uses `symbol` plus board where needed;
- row updates should use grid transactions;
- changed cells may flash for 500-900ms;
- connection state is visible in the status bar;
- stale data warning appears when no update arrives after the configured threshold.

Avoid noisy animations. Realtime feedback should help users notice change without making the board hard to read.

## Core Components

### Top Bar

Contains:

- `InvestView` product mark as text;
- market/session status;
- data source badge: `Mock`, `DNSE REST`, or `DNSE + SignalR`;
- connection state;
- demo/simulated badge.

### Board Tabs

Initial tabs:

- `VN30`
- `HOSE`
- `HNX`
- `UPCOM`
- `Watchlist`

Inactive tabs are quiet. Active tab uses `market-surface-2` and a clear underline or border.

### Toolbar

Contains:

- symbol search input;
- board selector/filter;
- refresh icon button;
- settings icon button;
- `Đặt lệnh` action button when order ticket exists.

Use icons for refresh/settings. Use text for `Đặt lệnh` because it is a primary trading command.

### Market Grid

AG Grid should be themed to match the Tailwind tokens.

Implementation expectations:

- grid wrapper owns height;
- column definitions are separated from the component;
- value formatters are pure functions;
- cell class rules use semantic price classifications;
- `getRowId` is prepared for future realtime updates;
- no DNSE payload names appear in grid code.

### Empty, Loading, and Error States

- Loading: compact skeleton rows or a single centered loading line inside the board area.
- Empty: short message with selected board/symbol context.
- Error: visible error band with retry action.
- Do not use large illustration-style empty states.

## Accessibility

- Maintain visible focus ring for keyboard users.
- Toolbar controls need accessible labels.
- Do not encode price movement by color alone. Use signs in change columns: `+`, `-`, or `0`.
- Keep contrast readable on the dark background.
- Do not use text smaller than `11px`.

## Tailwind Usage Rules

Do:

- prefer semantic classes backed by configured tokens;
- compose dense layouts with flex/grid utilities;
- keep repeated component styles in small components or utility functions;
- use `tabular-nums` and right alignment for numeric cells;
- use CSS variables for AG Grid theme bridges when Tailwind classes cannot reach internal grid elements.

Do not:

- scatter raw hex values through components;
- build large strings of one-off arbitrary values unless a token is not justified yet;
- use decorative gradients or background blobs;
- use Tailwind to override AG Grid internals randomly from many files;
- mix multiple UI libraries for basic controls.

## Implementation Checklist

Before coding Task 5:

- [ ] Install and configure Tailwind CSS for `src/investview.web`.
- [ ] Add AG Grid Community and AG Grid React.
- [ ] Add market design tokens to Tailwind config or CSS variables.
- [ ] Create quote formatting and price classification helpers with tests.
- [ ] Create `features/market-board` API mapping separate from AG Grid component.
- [ ] Build the board with grouped headers and pinned symbol column.
- [ ] Verify desktop and mobile screenshots manually.

## Boundaries

Always do:

- Build a securities-style workstation, not a marketing page.
- Keep visual decisions consistent with this file.
- Keep mock data clearly distinguishable from realtime market data.
- Use InvestView-owned API types and mapping functions.
- Preserve dense scanning and stable layout over decorative styling.

Ask first:

- Adding a paid UI/grid feature.
- Adding brand assets from SSI, DNSE, FSS, or any brokerage.
- Changing away from Tailwind CSS or AG Grid.
- Adding a second component library.

Never do:

- Clone SSI iBoard pixel-for-pixel.
- Use SSI logos, proprietary icons, or copyrighted images.
- Expose DNSE credentials in frontend code.
- Present simulated orders or mock quotes as real trading.
- Let AG Grid-specific types leak into backend contracts.
