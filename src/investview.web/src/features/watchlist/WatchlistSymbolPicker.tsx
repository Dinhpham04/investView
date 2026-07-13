import { useEffect, useRef, useState } from 'react';
import { useWatchlist } from './useWatchlist';
import type { WatchlistGroup } from '../../shared/types/watchlist';

type WatchlistSymbolPickerProps = {
  boardId: string;
  symbol: string;
};

export function WatchlistSymbolPicker({ boardId, symbol }: WatchlistSymbolPickerProps) {
  const rootRef = useRef<HTMLDivElement | null>(null);
  const [isOpen, setIsOpen] = useState(false);
  const {
    addItem,
    error,
    groups,
    isAdding,
    isLoading,
    session,
    status,
  } = useWatchlist();
  const normalizedSymbol = symbol.trim().toUpperCase();
  const normalizedBoardId = boardId.trim().toUpperCase();
  const isTracked = groups.some((group) => hasSymbol(group, normalizedBoardId, normalizedSymbol));

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

  const handleSelectGroup = async (group: WatchlistGroup) => {
    if (hasSymbol(group, normalizedBoardId, normalizedSymbol)) {
      setIsOpen(false);
      return;
    }

    try {
      await addItem({
        boardId: normalizedBoardId,
        groupId: group.id,
        symbol: normalizedSymbol,
      });
      setIsOpen(false);
    } catch {
      // The mutation exposes the error state below the picker.
    }
  };

  return (
    <div className="relative" ref={rootRef}>
      <button
        aria-expanded={isOpen}
        aria-haspopup="dialog"
        aria-label={`Theo dõi ${normalizedSymbol}`}
        className={`grid size-9 place-items-center rounded-sm border text-lg leading-none transition-colors ${
          isTracked
            ? 'border-[#f4c430] bg-[#3a3120] text-[#f4c430]'
            : 'border-[#504a5f] bg-[#252236] text-[#c8c6d4] hover:border-[#f4c430] hover:text-[#f4c430]'
        }`}
        title={isTracked ? `${normalizedSymbol} đã có trong danh mục` : `Thêm ${normalizedSymbol} vào danh mục`}
        type="button"
        onClick={() => setIsOpen((value) => !value)}
      >
        {isTracked ? '★' : '☆'}
      </button>

      {isOpen ? (
        <div
          aria-label={`Chọn danh mục cho ${normalizedSymbol}`}
          className="absolute right-0 top-full z-[180] mt-1 w-72 border border-market-border bg-[#211d30] shadow-2xl"
          role="dialog"
        >
          <div className="border-b border-[#353141] px-3 py-2">
            <p className="text-[12px] font-bold text-white">Thêm vào danh mục</p>
            <p className="text-[11px] font-semibold text-[#a7a1b5]">{normalizedSymbol} · {normalizedBoardId}</p>
          </div>

          <div className="max-h-64 overflow-y-auto p-2">
            {status === 'checking' || isLoading ? (
              <PickerState label="Đang tải danh mục" />
            ) : session == null ? (
              <PickerState label="Đăng nhập demo để thêm mã vào danh mục." />
            ) : groups.length === 0 ? (
              <PickerState label="Chưa có danh mục. Hãy tạo danh mục ở bảng giá trước." />
            ) : (
              <ul className="space-y-1" role="list">
                {groups.map((group) => {
                  const alreadyAdded = hasSymbol(group, normalizedBoardId, normalizedSymbol);

                  return (
                    <li key={group.id}>
                      <button
                        className={`flex min-h-9 w-full items-center justify-between gap-3 rounded-sm px-3 py-2 text-left text-[12px] font-bold transition-colors ${
                          alreadyAdded
                            ? 'bg-[#3a3120] text-[#f4c430]'
                            : 'text-[#e7e2ef] hover:bg-[#312b40]'
                        }`}
                        disabled={isAdding}
                        type="button"
                        onClick={() => {
                          void handleSelectGroup(group);
                        }}
                      >
                        <span className="min-w-0 truncate">{group.name}</span>
                        <span className="shrink-0 text-[11px] font-semibold text-[#a7a1b5]">
                          {alreadyAdded ? 'Đã có' : `${group.items.length} mã`}
                        </span>
                      </button>
                    </li>
                  );
                })}
              </ul>
            )}
          </div>

          {error instanceof Error ? (
            <p className="border-t border-[#353141] px-3 py-2 text-[11px] font-semibold text-state-error" role="alert">
              {error.message}
            </p>
          ) : null}
        </div>
      ) : null}
    </div>
  );
}

function PickerState({ label }: { label: string }) {
  return (
    <div className="grid min-h-16 place-items-center px-3 py-4 text-center text-[12px] font-semibold text-market-text-muted">
      {label}
    </div>
  );
}

function hasSymbol(group: WatchlistGroup, boardId: string, symbol: string) {
  return group.items.some((item) =>
    item.boardId.toUpperCase() === boardId && item.symbol.toUpperCase() === symbol,
  );
}
