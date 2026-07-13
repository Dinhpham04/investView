import { useDemoSession } from './useDemoSession';

export function DemoSessionControls() {
  const {
    error,
    isLoggingIn,
    login,
    logout,
    session,
    status,
  } = useDemoSession();

  if (status === 'checking') {
    return (
      <span className="rounded-sm border border-market-border bg-market-surface-2 px-2 py-1 text-market-text-muted">
        Đang kiểm tra phiên
      </span>
    );
  }

  if (session == null) {
    return (
      <div className="flex items-center gap-2">
        {error != null ? (
          <span className="text-state-error" role="alert">Đăng nhập thất bại</span>
        ) : null}
        <button
          className="h-7 rounded-sm border border-market-border-strong bg-market-surface-2 px-3 text-market-text hover:border-focus-ring disabled:text-market-text-subtle"
          disabled={isLoggingIn}
          type="button"
          onClick={() => {
            void login().catch(() => undefined);
          }}
        >
          {isLoggingIn ? 'Đang đăng nhập...' : 'Đăng nhập demo'}
        </button>
      </div>
    );
  }

  return (
    <div className="flex items-center gap-2">
      <span
        className="rounded-sm border border-market-border bg-market-surface-2 px-2 py-1 text-market-text"
        title={session.user.email}
      >
        {session.user.displayName}
      </span>
      <button
        className="h-7 rounded-sm border border-market-border bg-market-surface-2 px-2 text-market-text-muted hover:border-focus-ring hover:text-market-text"
        type="button"
        onClick={logout}
      >
        Đăng xuất
      </button>
    </div>
  );
}
