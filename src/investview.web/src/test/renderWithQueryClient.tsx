import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render } from '@testing-library/react';
import type { ReactElement } from 'react';
import { DemoSessionProvider } from '../features/auth/DemoSessionProvider';

export function renderWithQueryClient(ui: ReactElement) {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: {
        retry: false,
      },
    },
  });

  return render(
    <QueryClientProvider client={queryClient}>
      <DemoSessionProvider>
        {ui}
      </DemoSessionProvider>
    </QueryClientProvider>,
  );
}
