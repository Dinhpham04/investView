import { useEffect, useRef, useState } from 'react';
import type { FormEvent } from 'react';
import { useWatchlist } from './useWatchlist';
import type { WatchlistGroup } from '../../shared/types/watchlist';

type WatchlistPanelProps = {
  onGroupChange?: (group: WatchlistGroup) => void;
  selectedGroupId?: string | null;
  onSelectGroup?: (group: WatchlistGroup) => void;
};

export function WatchlistPanel({ onGroupChange, selectedGroupId = null, onSelectGroup }: WatchlistPanelProps) {
  const rootRef = useRef<HTMLDivElement | null>(null);
  const [isOpen, setIsOpen] = useState(false);
  const [groupName, setGroupName] = useState('');
  const {
    createGroup,
    error,
    groups,
    isCreatingGroup,
    isLoading,
    isRemoving,
    removeItem,
    session,
    status,
  } = useWatchlist();
  const selectedGroup = groups.find((group) => group.id === selectedGroupId) ?? null;

  useEffect(() => {
    if (!isOpen) {
      return undefined;
    }

    const handlePointerDown = (event: PointerEvent) => {
      const target = event.target;
      if (target instanceof Node && rootRef.current?.contains(target)) {
        return;
      }

      setIsOpen(false);
    };

    document.addEventListener('pointerdown', handlePointerDown);
    return () => document.removeEventListener('pointerdown', handlePointerDown);
  }, [isOpen]);

  const handleCreateGroup = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const nextName = groupName.trim();
    if (!nextName) {
      return;
    }

    try {
      const createdGroup = await createGroup({ name: nextName });
      setGroupName('');
      onSelectGroup?.(createdGroup);
    } catch {
      // The mutation exposes the error state in the panel.
    }
  };

  const handleRemoveItem = async (symbol: string, boardId: string) => {
    if (selectedGroup == null) {
      return;
    }

    try {
      await removeItem({
        boardId,
        groupId: selectedGroup.id,
        symbol,
      });
      onGroupChange?.({
        ...selectedGroup,
        items: selectedGroup.items.filter((item) => item.boardId !== boardId || item.symbol !== symbol),
      });
    } catch {
      // The mutation exposes the error state in the panel.
    }
  };

  return (
    <div className="relative z-[100]" ref={rootRef}>
      <button
        aria-expanded={isOpen}
        aria-haspopup="dialog"
        className={`flex h-8 items-center gap-1.5 border px-3 !text-[12px] font-medium hover:bg-market-surface-2 hover:text-market-text ${
          selectedGroup == null
            ? 'border-transparent text-[#c8c6d4]'
            : 'border-state-online border-x-0 border-b-0 border-t-2 text-market-text'
        }`}
        type="button"
        onClick={() => setIsOpen((value) => !value)}
      >
        <span className="max-w-32 truncate">{selectedGroup?.name ?? 'Danh mục của tôi'}</span>
        <svg className="shrink-0" width="9" height="6" viewBox="0 0 10 6" fill="none" xmlns="http://www.w3.org/2000/svg">
          <path d="M1 1L5 5L9 1" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
        </svg>
      </button>

      {isOpen ? (
        <div
          aria-label="Danh mục theo dõi"
          className="absolute left-0 top-full mt-1 w-[340px] border border-market-border bg-market-surface shadow-xl"
          role="dialog"
        >
          <div className="border-b border-market-border px-3 py-2">
            <p className="text-[12px] font-bold text-market-text">Danh mục của tôi</p>
            <p className="text-[11px] font-medium text-market-text-muted">
              {session?.user.displayName ?? 'Demo user'}
            </p>
          </div>

          <div className="space-y-3 p-3">
            {status === 'checking' ? (
              <p className="text-[11px] font-semibold text-market-text-muted">Đang xác minh phiên đăng nhập...</p>
            ) : session == null ? (
              <p className="text-[11px] font-semibold text-market-text-muted">
                Đăng nhập ở góc trên bên phải để quản lý danh mục theo dõi.
              </p>
            ) : (
              <>
                <div className="max-h-56 overflow-y-auto border border-market-border bg-market-bg">
                  {isLoading ? (
                    <PanelState label="Đang tải danh mục" />
                  ) : groups.length === 0 ? (
                    <PanelState label="Chưa có danh mục" />
                  ) : (
                    <ul className="divide-y divide-market-border" role="list">
                      {groups.map((group) => (
                        <li key={group.id}>
                          <button
                            aria-pressed={selectedGroupId === group.id}
                            className={`flex min-h-10 w-full items-center justify-between gap-3 px-3 py-2 text-left text-[12px] font-bold hover:bg-[#312b40] ${
                              selectedGroupId === group.id ? 'bg-[#3c354d] text-white' : 'text-[#c8c6d4]'
                            }`}
                            type="button"
                            onClick={() => {
                              onSelectGroup?.(group);
                              setIsOpen(false);
                            }}
                          >
                            <span className="min-w-0 truncate">{group.name}</span>
                            <span className="shrink-0 text-[11px] font-semibold text-market-text-muted">
                              {group.items.length} mã
                            </span>
                          </button>
                        </li>
                      ))}
                    </ul>
                  )}
                </div>

                {selectedGroup ? (
                  <section className="border border-market-border bg-market-bg" aria-label={`Mã trong ${selectedGroup.name}`}>
                    <div className="flex items-center justify-between border-b border-market-border px-3 py-2">
                      <p className="text-[12px] font-bold text-market-text">{selectedGroup.name}</p>
                      <span className="text-[11px] font-semibold text-market-text-muted">{selectedGroup.items.length} mã</span>
                    </div>

                    {selectedGroup.items.length === 0 ? (
                      <PanelState label="Danh mục chưa có mã" />
                    ) : (
                      <ul className="max-h-44 divide-y divide-market-border overflow-y-auto" role="list">
                        {selectedGroup.items.map((item) => (
                          <li className="flex min-h-9 items-center justify-between gap-3 px-3 py-2" key={item.id}>
                            <div className="min-w-0">
                              <p className="truncate text-[12px] font-extrabold text-market-text">{item.symbol}</p>
                              <p className="text-[11px] font-semibold text-market-text-muted">{item.boardId}</p>
                            </div>
                            <button
                              aria-label={`Xóa ${item.symbol} khỏi ${selectedGroup.name}`}
                              className="grid size-6 shrink-0 place-items-center rounded-sm border border-transparent text-sm font-bold text-market-text-muted hover:border-state-error hover:text-state-error disabled:opacity-50"
                              disabled={isRemoving}
                              type="button"
                              onClick={() => {
                                void handleRemoveItem(item.symbol, item.boardId);
                              }}
                            >
                              ×
                            </button>
                          </li>
                        ))}
                      </ul>
                    )}
                  </section>
                ) : null}

                <form className="flex items-center gap-2" onSubmit={handleCreateGroup}>
                  <label className="sr-only" htmlFor="watchlist-group-name">
                    Tên danh mục
                  </label>
                  <input
                    className="h-8 min-w-0 flex-1 rounded border border-market-border bg-market-surface-2 px-2 text-[12px] font-bold text-market-text outline-none placeholder:text-market-text-subtle focus:border-focus-ring"
                    id="watchlist-group-name"
                    onChange={(event) => setGroupName(event.target.value)}
                    placeholder="Tạo danh mục mới"
                    type="text"
                    value={groupName}
                  />
                  <button
                    aria-label="Tạo danh mục"
                    className="grid h-8 w-8 place-items-center border border-market-border-strong bg-market-surface-2 text-lg font-bold leading-none text-market-text hover:border-focus-ring disabled:text-market-text-subtle"
                    disabled={isCreatingGroup || groupName.trim().length === 0}
                    type="submit"
                  >
                    +
                  </button>
                </form>
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
