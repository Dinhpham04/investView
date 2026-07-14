import { useCallback, useState } from 'react';
import { XIcon } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Sheet, SheetClose, SheetContent, SheetTitle } from '@/components/ui/sheet';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import type { MarketQuote, MarketSessionUpdate } from '../../shared/types/market';
import { OrderTicketPanel } from '../order-ticket/OrderTicketPanel';
import type { OrderTicketPreset } from '../order-ticket/OrderTicketPanel';
import type { SymbolDetailSelection } from '../symbol-detail/useSymbolDetailQueries';

type TradingDrawerProps = {
  isOpen: boolean;
  liveQuote: MarketQuote | null;
  marketSession?: MarketSessionUpdate | null;
  onClose: () => void;
  orderPreset?: OrderTicketPreset | null;
  selection: SymbolDetailSelection | null;
};

type TradingMode = 'spot' | 'conditional';

const tradingTabs: Array<{ id: TradingMode; label: string }> = [
  { id: 'spot', label: 'Giao dịch cơ sở' },
  { id: 'conditional', label: 'Đặt lệnh điều kiện' },
];

export function TradingDrawer({ isOpen, liveQuote, marketSession = null, onClose, orderPreset = null, selection }: TradingDrawerProps) {
  const [activeMode, setActiveMode] = useState<TradingMode>('spot');
  const closeDrawer = useCallback(() => {
    setActiveMode('spot');
    onClose();
  }, [onClose]);

  return (
    <Sheet
      open={isOpen}
      onOpenChange={(open) => {
        if (!open) {
          closeDrawer();
        }
      }}
    >
      <SheetContent
        aria-label="Bảng đặt lệnh mô phỏng"
        className="z-[221] h-full max-w-[560px] gap-0 border-[#373347] bg-[#1b1828] p-0 text-[#efedf5] data-[side=right]:w-[min(100vw,560px)] data-[side=right]:sm:max-w-[560px]"
        data-testid="trading-drawer"
        showCloseButton={false}
        side="right"
      >
        <SheetTitle className="sr-only">Bảng đặt lệnh mô phỏng</SheetTitle>

        <Tabs
          className="h-full min-h-0 gap-0"
          value={activeMode}
          onValueChange={(value) => setActiveMode(value as TradingMode)}
        >
          <header className="flex h-[35px] shrink-0 items-stretch border-b border-[#393548] bg-[#1d1a2b]">
            <TabsList
              aria-label="Loại giao dịch"
              className="h-full min-w-0 flex-1 gap-0 rounded-none bg-transparent p-0"
              variant="line"
            >
              {tradingTabs.map((tab) => (
                <TabsTrigger
                  className="h-full rounded-none px-3 py-0 text-[12px] font-semibold text-[#b8b5c0] after:bottom-0 after:bg-[#ef3340] hover:text-white data-active:text-white"
                  key={tab.id}
                  value={tab.id}
                >
                  {tab.label}
                </TabsTrigger>
              ))}
            </TabsList>
            <SheetClose asChild>
              <Button
                aria-label="Đóng bảng đặt lệnh"
                className="h-full w-10 shrink-0 rounded-none text-[#c7c3ce] hover:bg-white/5 hover:text-white"
                size="icon"
                type="button"
                variant="ghost"
              >
                <XIcon className="size-[18px]" />
              </Button>
            </SheetClose>
          </header>

          <TabsContent className="flex min-h-0 flex-col overflow-hidden" value="spot">
            <OrderTicketPanel liveQuote={liveQuote} marketSession={marketSession} preset={orderPreset} selection={selection} />
          </TabsContent>
          <TabsContent className="flex min-h-0 flex-col" value="conditional">
            <UnavailableTradingMode />
          </TabsContent>
        </Tabs>
      </SheetContent>
    </Sheet>
  );
}

function UnavailableTradingMode() {
  return (
    <div className="grid h-full place-items-center px-8 text-center">
      <div>
        <p className="text-sm font-bold text-white">Đặt lệnh điều kiện</p>
        <p className="mt-2 text-xs leading-5 text-[#aaa7b3]">
          Chức năng này chưa được hỗ trợ trong tài khoản mô phỏng.
        </p>
      </div>
    </div>
  );
}
