import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// Aspire WithReference injects AUTH_API_HTTP/HTTPS and QUOTES_API_HTTP/HTTPS for resources auth-api / quotes-api.
const authTarget = process.env.AUTH_API_HTTPS || process.env.AUTH_API_HTTP;
const quotesTarget = process.env.QUOTES_API_HTTPS || process.env.QUOTES_API_HTTP;

export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      '/api/auth': {
        target: authTarget,
        changeOrigin: true,
        secure: false,
      },
      '/api/quotes': {
        target: quotesTarget,
        changeOrigin: true,
        secure: false,
      },
    },
  },
});
