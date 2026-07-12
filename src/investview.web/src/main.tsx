import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { QueryClientProvider } from '@tanstack/react-query'
import { App } from './app/App'
import { createQueryClient } from './app/queryClient'
import { DemoSessionProvider } from './features/auth/DemoSessionProvider'
import './index.css'

const queryClient = createQueryClient()

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <DemoSessionProvider>
        <App />
      </DemoSessionProvider>
    </QueryClientProvider>
  </StrictMode>,
)
