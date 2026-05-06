import React from 'react';
import ReactDOM from 'react-dom/client';
import { BrowserRouter } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { initI18n } from '@kfs/i18n';
import { Toaster } from 'sonner';

import '@fontsource-variable/source-sans-3';
import '@fontsource/ibm-plex-sans-arabic/400.css';
import '@kfs/ui/styles.css';

import App from './App';

initI18n({ defaultLocale: 'en', app: 'admin' });

const queryClient = new QueryClient({
  defaultOptions: { queries: { retry: 1, refetchOnWindowFocus: false } }
});

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <App />
      </BrowserRouter>
      <Toaster richColors position="top-center" />
    </QueryClientProvider>
  </React.StrictMode>
);
