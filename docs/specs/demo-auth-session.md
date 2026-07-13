# Spec: Demo authentication session

## Objective

Make demo authentication a single app-level concern. A guest can sign in from the top-right header, an authenticated demo user can sign out there, and a stored or active token rejected by the API returns the application to the guest state instead of leaving protected features stuck on `401 Unauthorized`.

## Tech Stack

- React 19 and TypeScript
- TanStack Query for server-state cache
- Browser `fetch` and `localStorage`
- Vitest and Testing Library
- Existing ASP.NET Core `/api/auth/demo-login` and `/api/me` endpoints

## Commands

- Test focused auth behavior: `npm test -- --run src/features/auth`
- Test frontend: `npm test`
- Lint frontend: `npm run lint`
- Build frontend: `npm run build`

Run the commands from `src/investview.web`.

## Project Structure

- `src/investview.web/src/features/auth/` owns demo-session state and header controls.
- `src/investview.web/src/shared/api/` owns HTTP errors and auth API calls.
- `src/investview.web/src/app/App.tsx` composes the auth controls into the app header.
- Co-located `*.test.ts(x)` files protect session and UI behavior.

## Code Style

Use explicit status values instead of inferring authentication from a nullable token:

```ts
type DemoSessionStatus = 'checking' | 'guest' | 'authenticated';
```

Keep HTTP transport concerns in `shared/api`, session transitions in the provider, and rendering in a small auth control component.

## Testing Strategy

- Unit-test that a `401` response notifies auth subscribers while retaining the HTTP status.
- Component-test that a rejected stored session is removed and becomes guest state.
- Component-test header login and logout outcomes.
- Run the existing portfolio, order-ticket, and watchlist tests to detect protected-feature regressions.

## Boundaries

- Always: treat the backend as the authority for session validity; clear protected query caches on logout; preserve existing demo-login endpoint and credentials.
- Ask first: add refresh tokens, a real registration flow, or a new authentication dependency.
- Never: display a rejected token as authenticated; log or expose access tokens; alter simulated-order business rules.

## Success Criteria

- A guest sees `Đăng nhập demo` in the top-right header.
- A valid demo session shows the display name and `Đăng xuất` in the header.
- A stored token is checked against `/api/me` before protected queries are enabled.
- Any API `401` clears the stored session and protected query caches.
- A rejected stored token returns the UI to guest state without leaving `401 Unauthorized` as the persistent portfolio state.
- Frontend tests, lint, and build pass.

## Open Questions

- Real `Đăng ký` remains out of scope because no registration endpoint exists yet.
