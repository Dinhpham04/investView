import type { ReactNode } from 'react';
import { InfoIcon, SearchIcon } from 'lucide-react';
import { Button } from '@/components/ui/button';
import {
  InputGroup,
  InputGroupAddon,
  InputGroupButton,
  InputGroupInput,
} from '@/components/ui/input-group';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';
import type { MarketQuote } from '../../shared/types/market';
import {
  classifyChange,
  formatChange,
  formatPercent,
  formatPrice,
  formatQuantity,
} from '../market-board/marketBoardFormatters';
import type { SymbolDetailSelection } from '../symbol-detail/useSymbolDetailQueries';

type QuoteHeaderProps = {
  liveQuote: MarketQuote | null;
  selection: SymbolDetailSelection | null;
};

export function QuoteHeader({ liveQuote, selection }: QuoteHeaderProps) {
  const changeTone = classifyChange(liveQuote?.change);
  const priceToneClass = changeTone === 'up'
    ? 'text-[#16d982]'
    : changeTone === 'down'
      ? 'text-[#ff3045]'
      : 'text-[#ffd41c]';

  return (
    <div className="grid h-[61px] shrink-0 grid-cols-[1fr_auto] items-center gap-3 px-2">
      <div className="flex min-w-0 items-center gap-1.5">
        <span className="grid size-[26px] shrink-0 place-items-center rounded border border-[#4a465d] text-[#d2ced9]" aria-hidden="true">
          <SearchIcon className="size-[15px]" />
        </span>
        <div className="flex shrink-0 items-baseline gap-2">
          <strong className="text-[18px] leading-none text-white">{selection?.symbol ?? '--'}</strong>
          <span className="text-[10px] font-semibold text-[#b3afbb]">({liveQuote?.marketId ?? selection?.boardId ?? '--'})</span>
        </div>
        <div className="ml-auto min-w-0 text-right">
          <p className={`whitespace-nowrap text-[13px] font-bold ${priceToneClass}`}>
            {formatPrice(liveQuote?.lastPrice)} ({formatChange(liveQuote?.change, liveQuote?.referencePrice)} {formatPercent(liveQuote?.changePercent)})
          </p>
          <p className="mt-0.5 flex justify-end gap-3 font-bold">
            <span className="text-[#dc35fa]">{formatPrice(liveQuote?.ceilingPrice)}</span>
            <span className="text-[#ffe000]">{formatPrice(liveQuote?.referencePrice)}</span>
            <span className="text-[#1ed8f5]">{formatPrice(liveQuote?.floorPrice)}</span>
            <InfoMark label="Giá trần, tham chiếu và giá sàn" />
          </p>
        </div>
      </div>
      <div className="min-w-[116px] text-right leading-[17px]">
        <p className="whitespace-nowrap font-semibold text-[#e2dee7]">
          <span className="mr-1 text-[#ffbf21]">●</span>{formatTradingStatus(liveQuote?.tradingStatus)}
        </p>
        <p className="whitespace-nowrap text-[#bbb7c3]">Tổng KL <strong className="text-white">{formatQuantity(liveQuote?.totalVolume)}</strong></p>
      </div>
    </div>
  );
}

export function FieldRow({
  children,
  label,
  withInfo = false,
}: {
  children: ReactNode;
  label: string;
  withInfo?: boolean;
}) {
  return (
    <div className="grid min-h-[31px] grid-cols-[144px_minmax(0,1fr)] items-center gap-2 px-2">
      <div className="flex items-center gap-1 text-[#bcb8c4]">
        <span>{label}</span>
        {withInfo ? <InfoMark label={`Thông tin ${label.toLowerCase()}`} /> : null}
      </div>
      {children}
    </div>
  );
}

export function StepperInput({
  ariaLabel,
  id,
  onChange,
  step,
  value,
}: {
  ariaLabel: string;
  id: string;
  onChange: (value: string) => void;
  step: number;
  value: string;
}) {
  const updateValue = (direction: -1 | 1) => {
    const parsedValue = Number(value.replace(',', '.') || 0);
    const currentValue = Number.isFinite(parsedValue) ? parsedValue : 0;
    const nextValue = Math.max(0, currentValue + direction * step);
    onChange(nextValue === 0 ? '' : String(Number(nextValue.toFixed(2))));
  };

  return (
    <InputGroup className="h-[28px] overflow-hidden rounded border-[#4a465d] bg-[#1e1b2b] shadow-none outline-none ring-0 focus-within:border-[#77718e] focus-within:outline-none focus-within:ring-0 has-[[data-slot=input-group-control]:focus-visible]:border-[#77718e] has-[[data-slot=input-group-control]:focus-visible]:ring-0 dark:bg-[#1e1b2b]">
      <InputGroupAddon align="inline-start" className="p-0">
        <InputGroupButton
          aria-label={`Giảm ${ariaLabel.toLowerCase()}`}
          className="h-full w-8 rounded-none text-[#8e899b] hover:bg-white/5 hover:text-white"
          size="icon-xs"
          onClick={() => updateValue(-1)}
        >
          −
        </InputGroupButton>
      </InputGroupAddon>
      <InputGroupInput
        aria-label={ariaLabel}
        className="h-full min-w-0 flex-1 rounded-none border-0 bg-transparent px-1 text-right text-[12px] text-white shadow-none outline-none ring-0 placeholder:text-[#7c778c] focus:border-0 focus:outline-none focus:ring-0 focus-visible:border-0 focus-visible:outline-none focus-visible:ring-0 dark:bg-transparent"
        id={id}
        inputMode="decimal"
        placeholder="–"
        type="text"
        value={value}
        onChange={(event) => onChange(event.target.value)}
      />
      <InputGroupAddon align="inline-end" className="p-0">
        <InputGroupButton
          aria-label={`Tăng ${ariaLabel.toLowerCase()}`}
          className="h-full w-8 rounded-none text-[#aaa5b4] hover:bg-white/5 hover:text-white"
          size="icon-xs"
          onClick={() => updateValue(1)}
        >
          +
        </InputGroupButton>
      </InputGroupAddon>
    </InputGroup>
  );
}

function InfoMark({ label }: { label: string }) {
  return (
    <Tooltip>
      <TooltipTrigger asChild>
        <Button
          aria-label={label}
          className="size-4 rounded-full text-current hover:bg-white/5 hover:text-white"
          size="icon-xs"
          type="button"
          variant="ghost"
        >
          <InfoIcon className="size-3" />
        </Button>
      </TooltipTrigger>
      <TooltipContent className="bg-[#efedf5] text-[#1b1828]" sideOffset={5}>
        {label}
      </TooltipContent>
    </Tooltip>
  );
}

function formatTradingStatus(status: string | null | undefined) {
  if (status == null || status.trim() === '') {
    return 'Chưa có phiên';
  }

  return status.toLowerCase() === 'continuous' ? 'Phiên liên tục' : status;
}
