import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';

// Aspire WithReference injects AUTH_API_HTTP/HTTPS and QUOTES_API_HTTP/HTTPS for resources auth-api / quotes-api.
const authTarget = process.env.AUTH_API_HTTPS || process.env.AUTH_API_HTTP;
const quotesTarget = process.env.QUOTES_API_HTTPS || process.env.QUOTES_API_HTTP;

export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      '/api/v1/auth': {
        target: authTarget,
        changeOrigin: true,
        secure: false,
      },
      '/api/v1/quotes': {
        target: quotesTarget,
        changeOrigin: true,
        secure: false,
      },
    },
  },
  test: {
    environment: 'jsdom',
    globals: true,
    include: ['src/**/*.test.{ts,tsx}'],
    setupFiles: ['src/test/setup.ts'],
    clearMocks: true,
    restoreMocks: true,
    unstubGlobals: true,
    coverage: {
      provider: 'v8',
      // Sonar reads lcov; text keeps the terminal run readable.
      reporter: ['text', 'lcov'],
      reportsDirectory: 'coverage',
      include: ['src/**/*.{ts,tsx}'],
      exclude: ['src/**/*.test.{ts,tsx}', 'src/test/**', 'src/main.tsx', 'src/vite-env.d.ts'],
    },
  },
});
