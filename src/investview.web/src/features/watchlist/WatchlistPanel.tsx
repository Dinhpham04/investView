import { useState } from 'react';
import type { FormEvent } from 'react';
import { useWatchlist } from './useWatchlist';

const defaultBoardId = 'G1';

export function WatchlistPanel() {
  const [isOpen, setIsOpen] = useState(false);
  const [symbol, setSymbol] = useState('');
  const {
    addItem,
    error,
    isAdding,
    isLoading,
    isLoggingIn,
    isRemoving,
    items,
    login,
    removeItem,
    session,
  } = useWatchlist();

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const nextSymbol = symbol.trim();
    if (!nextSymbol) {
      return;
    }

    try {
      await addItem({ symbol: nextSymbol, boardId: defaultBoardId });
      setSymbol('');
    } catch {
      // The mutation exposes the error state in the panel.
    }
  };

  return (
    <div className="relative z-[100]">
      <button
        aria-expanded={isOpen}
        aria-haspopup="dialog"
        className="flex h-8 items-center gap-1.5 border border-transparent px-3 text-[12px] font-medium text-[#c8c6d4] hover:bg-market-surface-2 hover:text-market-text"
        type="button"
        onClick={() => setIsOpen((value) => !value)}
      >
        Danh muc cua toi
        <svg className="shrink-0" width="9" height="6" viewBox="0 0 10 6" fill="none" xmlns="http://www.w3.org/2000/svg">
          <path d="M1 1L5 5L9 1" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
        </svg>
      </button>

      {isOpen ? (
        <div
          aria-label="Danh muc theo doi"
          className="absolute left-0 top-full mt-1 w-[340px] border border-market-border bg-market-surface shadow-xl"
          role="dialog"
        >
          <div className="border-b border-market-border px-3 py-2">
            <p className="text-[12px] font-bold text-market-text">Danh muc theo doi</p>
            <p className="text-[11px] font-medium text-market-text-muted">
              {session?.user.displayName ?? 'Demo user'}
            </p>
          </div>

          <div className="space-y-3 p-3">
            {session == null ? (
              <button
                className="h-8 w-full border border-market-border-strong bg-market-surface-2 px-3 text-[12px] font-bold text-market-text hover:border-focus-ring"
                disabled={isLoggingIn}
                type="button"
                onClick={() => {
                  void login().catch(() => undefined);
                }}
              >
                {isLoggingIn ? 'Dang nhap...' : 'Dang nhap demo'}
              </button>
            ) : (
              <>
                <form className="flex items-end gap-2" onSubmit={handleSubmit}>
                  <div className="min-w-0 flex-1">
                    <label className="mb-1 block text-[11px] font-bold uppercase text-market-text-muted" htmlFor="watchlist-symbol">
                      Ma CK
                    </label>
                    <input
                      className="h-8 w-full rounded border border-market-border bg-market-surface-2 px-2 text-[12px] font-bold uppercase text-market-text outline-none placeholder:text-market-text-subtle focus:border-focus-ring"
                      id="watchlist-symbol"
                      onChange={(event) => setSymbol(event.target.value)}
                      placeholder="HPG"
                      type="text"
                      value={symbol}
                    />
                  </div>
                  <button
                    className="h-8 border border-market-border-strong bg-market-surface-2 px-3 text-[12px] font-bold text-market-text hover:border-focus-ring disabled:text-market-text-subtle"
                    disabled={isAdding || symbol.trim().length === 0}
                    type="submit"
                  >
                    {isAdding ? 'Dang them' : 'Them'}
                  </button>
                </form>

                <div className="max-h-64 overflow-y-auto border border-market-border bg-market-bg">
                  {isLoading ? (
                    <PanelState label="Dang tai danh muc" />
                  ) : items.length === 0 ? (
                    <PanelState label="Chua co ma theo doi" />
                  ) : (
                    <ul className="divide-y divide-market-border" role="list">
                      {items.map((item) => (
                        <li className="flex min-h-9 items-center justify-between gap-3 px-2 py-1.5" key={`${item.boardId}:${item.symbol}`}>
                          <div className="min-w-0">
                            <p className="truncate text-[12px] font-extrabold text-market-text">{item.symbol}</p>
                            <p className="text-[11px] font-medium text-market-text-muted">{item.boardId}</p>
                          </div>
                          <button
                            aria-label={`Xoa ${item.symbol} ${item.boardId}`}
                            className="h-7 border border-market-border px-2 text-[11px] font-bold text-state-error hover:border-state-error disabled:text-market-text-subtle"
                            disabled={isRemoving}
                            type="button"
                            onClick={() => {
                              void removeItem({ symbol: item.symbol, boardId: item.boardId }).catch(() => undefined);
                            }}
                          >
                            Xoa
                          </button>
                        </li>
                      ))}
                    </ul>
                  )}
                </div>
              </>
            )}

            {error instanceof Error ? (
              <p className="text-[11px] font-semibold text-state-error" role="alert">
                {error.message}
              </p>
            ) : null}
          </div>
        </div>
      ) : null}
    </div>
  );
}

function PanelState({ label }: { label: string }) {
  return (
    <div className="grid min-h-16 place-items-center px-3 py-4 text-center text-[12px] font-semibold text-market-text-muted">
      {label}
    </div>
  );
}
