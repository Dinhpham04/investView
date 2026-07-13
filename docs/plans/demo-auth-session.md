# Kế hoạch triển khai: Demo authentication session

## Architecture Decisions

- HTTP exposes a typed error and a small unauthorized-subscription mechanism; it does not import React or feature modules.
- `DemoSessionProvider` is the single owner of checking, login, logout, storage, and protected-cache cleanup.
- Stored sessions are verified through the existing `/api/me` endpoint before being exposed to consumers.
- Header authentication UI is isolated in `DemoSessionControls` and composed by `App`.

## Task List

### Task 1: Propagate unauthorized responses

- Acceptance: a `401` produces an error containing status `401` and notifies current subscribers once.
- Verification: focused Vitest test passes.
- Files: `shared/api/httpClient.ts`, `shared/api/httpClient.test.ts`.

### Task 2: Make demo session authoritative

- Acceptance: stored session starts in checking state; `/api/me` success authenticates it; rejection removes storage; logout removes protected caches.
- Verification: provider component tests pass.
- Files: auth API, auth context/provider, provider tests.
- Dependency: Task 1.

### Task 3: Put demo authentication in the header

- Acceptance: guest can log in from the header; authenticated user can log out; checking state is visible without exposing protected UI as authenticated.
- Verification: auth-control component tests and existing feature tests pass.
- Files: `DemoSessionControls.tsx`, its test, `App.tsx`.
- Dependency: Task 2.

### Checkpoint

- Run all frontend tests.
- Run lint and production build.
- Review correctness, readability, architecture, security, and performance.

## Risks and Mitigations

- Transient `/api/me` failure signs the demo user out: safe default for a demo account; real auth can later distinguish offline and invalid-token states.
- Multiple protected requests may emit `401`: logout is idempotent and query caches are removed by key prefix.
- React Strict Mode runs effects twice in development: validation uses effect cleanup and the unauthorized subscription is removable.
