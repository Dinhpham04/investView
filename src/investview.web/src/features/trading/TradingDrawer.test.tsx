import { fireEvent, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { renderWithQueryClient } from '../../test/renderWithQueryClient';
import { TradingDrawer } from './TradingDrawer';

describe('TradingDrawer', () => {
  it('uses shadcn Sheet and supports modal close interactions', () => {
    const onClose = vi.fn();
    const { unmount } = renderWithQueryClient(
      <TradingDrawer isOpen liveQuote={null} onClose={onClose} selection={null} />,
    );

    const drawer = screen.getByTestId('trading-drawer');
    expect(drawer).toHaveAttribute('role', 'dialog');
    expect(drawer).toHaveAttribute('data-slot', 'sheet-content');
    expect(drawer).toHaveClass('max-w-[560px]');
    expect(screen.getByRole('tab', { name: 'Giao dịch cơ sở' })).toHaveAttribute('aria-selected', 'true');
    expect(screen.getByRole('tab', { name: 'Giao dịch cơ sở' })).toHaveAttribute('data-slot', 'tabs-trigger');
    expect(screen.queryByRole('tab', { name: 'Giao dịch phái sinh' })).not.toBeInTheDocument();
    expect(screen.getByRole('tab', { name: 'Đặt lệnh điều kiện' })).toBeInTheDocument();
    fireEvent.keyDown(document, { key: 'Escape' });
    expect(onClose).toHaveBeenCalledOnce();

    expect(document.querySelector('[data-slot="sheet-overlay"]')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Đóng bảng đặt lệnh' }));
    expect(onClose).toHaveBeenCalledTimes(2);

    unmount();
  });

  it('switches from spot to conditional orders through shadcn Tabs', () => {
    renderWithQueryClient(
      <TradingDrawer isOpen liveQuote={null} onClose={vi.fn()} selection={null} />,
    );

    const conditionalTab = screen.getByRole('tab', { name: 'Đặt lệnh điều kiện' });
    fireEvent.mouseDown(conditionalTab, { button: 0, ctrlKey: false });
    fireEvent.click(conditionalTab);

    expect(screen.getByText('Chức năng này chưa được hỗ trợ trong tài khoản mô phỏng.')).toBeInTheDocument();
    expect(conditionalTab).toHaveAttribute('aria-selected', 'true');
  });
});
