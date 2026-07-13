# Spec: Watchlist groups for market board

## Objective

Cho phép user tạo nhiều danh mục theo dõi, thêm mã chứng khoán vào từng danh mục, và chọn một danh mục để lọc bảng giá theo đúng các mã trong danh mục đó.

Phase hiện tại triển khai:

- Phase 1: Backend lưu được danh mục và item theo từng danh mục.
- Phase 2: Frontend tạo/chọn danh mục và lọc MarketBoard theo danh mục.
- Phase 3: Symbol Detail có nút ngôi sao để thêm mã đang xem vào danh mục được chọn.
- Phase 4: Dropdown danh mục hiển thị/quản lý các mã trong danh mục và cho phép xóa mã.

Nút ngôi sao trong Symbol Detail và quản lý mã trong danh mục thuộc lát cắt Phase 3/4.

## Commands

- Backend test: `dotnet test InvestView.sln --filter Watchlist`
- Backend build: `dotnet build InvestView.sln`
- Frontend test: `npm test -- --run src/features/watchlist/WatchlistPanel.test.tsx src/features/market-board/MarketBoard.test.tsx`
- Frontend lint: `npm run lint`

## Data model

- `WatchlistGroup`
  - `Id`
  - `UserId`
  - `Name`
  - `CreatedAt`
  - `UpdatedAt`
- `WatchlistItem`
  - `Id`
  - `GroupId`
  - `Symbol`
  - `BoardId`
  - `CreatedAt`

Unique constraints:

- `WatchlistGroup`: `(UserId, Name)`
- `WatchlistItem`: `(GroupId, BoardId, Symbol)`

## API contract

- `GET /api/watchlist`: list groups with items.
- `POST /api/watchlist`: create group by name.
- `POST /api/watchlist/{groupId}/items`: add symbol to group.
- `DELETE /api/watchlist/{groupId}/items/{boardId}/{symbol}`: remove symbol from group.

## Frontend behavior

- Dropdown "Danh mục của tôi" lists user-created groups.
- User can create a group from the dropdown.
- Selecting a group changes MarketBoard filter to `watchlist`.
- MarketBoard requests quotes by `symbols[]` from selected group.
- Empty group shows an empty board state.
- Symbol Detail header shows a star button when a symbol is selected.
- Clicking the star opens a group picker. Each group row shows whether the selected symbol is already in that group.
- Selecting a group calls `POST /api/watchlist/{groupId}/items` for the selected symbol. MarketBoard keeps the current filter unchanged.
- The watchlist dropdown shows symbols in the selected group and lets user remove a symbol through `DELETE /api/watchlist/{groupId}/items/{boardId}/{symbol}`.

## Task list

### Phase 3: Add symbol from Symbol Detail

- [x] Task 3.1: Add a `WatchlistSymbolPicker` UI component.
  - Acceptance: It is hidden when there is no login, lists groups when logged in, marks groups that already contain the selected symbol, and calls `addItem` for a selected group.
  - Verification: Component behavior covered through MarketBoard/SymbolDetail tests.
  - Files: `src/investview.web/src/features/watchlist/WatchlistSymbolPicker.tsx`, `src/investview.web/src/features/symbol-detail/SymbolDetailPanel.tsx`.
- [x] Task 3.2: Wire Symbol Detail star to MarketBoard.
  - Acceptance: User opens symbol detail, clicks star, chooses a group, and MarketBoard filters to that group.
  - Verification: Frontend test asserts POST add-item request and `/api/market/quotes?boardId=G1&symbols=...`.
  - Files: `SymbolDetailPanel.tsx`, `MarketBoard.tsx`, `MarketBoard.test.tsx`.

### Phase 4: Manage symbols in watchlist dropdown

- [x] Task 4.1: Show group symbols and remove actions in `WatchlistPanel`.
  - Acceptance: Selected group displays its symbols with a remove button for each symbol.
  - Verification: WatchlistPanel test asserts DELETE request and UI/cache update.
  - Files: `WatchlistPanel.tsx`, `WatchlistPanel.test.tsx`.
- [x] Task 4.2: Keep selected MarketBoard group synchronized after add/remove.
  - Acceptance: When the selected group's items change, MarketBoard active filter receives the latest group object and re-queries/clears quotes correctly.
  - Verification: MarketBoard tests cover add through star and remove through dropdown.
  - Files: `WatchlistPanel.tsx`, `WatchlistSymbolPicker.tsx`, `MarketBoard.tsx`, tests.

## Success criteria

- User can create a new watchlist group after demo login.
- User can add/remove symbols in a group through the API.
- Frontend can select a group and issue `/api/market/quotes?boardId=G1&symbols=...`.
- User can add the currently opened Symbol Detail symbol to a chosen group through the star button.
- User can remove a symbol from the selected group in the watchlist dropdown.
- Existing index/exchange filters keep working.
- Backend and frontend tests pass for the touched flows.
