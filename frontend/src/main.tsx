import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import './theme/tokens.css'
import './theme/base.css'
import './shared/ui/ui.css'
import './shared/ui/publico.css'
import './shared/ui/animaciones.css'
import './features/portal/portal.css'
import './features/usuarios/usuarios.css'
import './features/privacidad/privacidad.css'
import './features/bandeja/bandeja.css'
import './features/pruebas/pruebas.css'
import './features/candidatos/candidatos.css'
import './features/preseleccion/preseleccion.css'
import { App } from './app/App'

const queryClient = new QueryClient({
  defaultOptions: { queries: { staleTime: 30_000, retry: 1 } },
})

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <App />
    </QueryClientProvider>
  </StrictMode>,
)
