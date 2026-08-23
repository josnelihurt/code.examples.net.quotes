import { defineConfig, devices } from '@playwright/test';
import { defineBddConfig } from 'playwright-bdd';

const testDir = defineBddConfig({
  features: 'e2e/features/**/*.feature',
  steps: 'e2e/steps/**/*.ts',
});

// Shared by both APIs (Auth signs, Quotes verifies) — mirrors the CI smoke key the old
// bash script used; any 32+ char value the Production guard would accept works.
const JWT_KEY = 'e2e-signing-key-0123456789abcdef';

export default defineConfig({
  testDir,
  reporter: process.env.CI ? [['html', { open: 'never' }], ['list']] : 'html',
  use: { baseURL: 'http://127.0.0.1:5173', trace: 'on-first-retry' },
  projects: [{ name: 'chromium', use: devices['Desktop Chrome'] }],
  // The quote catalog lives in an in-memory singleton shared by every scenario of the
  // run, and browsing scenarios assert exact page counts over the seeded catalog — so
  // scenarios must not interleave. One worker keeps publishing-quotes from growing the
  // catalog while browsing-quotes is asserting on it.
  workers: 1,
  // The topology the old CI smoke job already proved: both APIs on fixed loopback ports
  // plus the Vite dev server, wired by env vars (vite.config.ts proxies /api/* to them).
  // The Aspire test host is deliberately not used here — its dynamic ports are invisible
  // to Node, and browser journeys need the real dev server anyway. Ports 5201/5202 sit
  // outside the range a running Aspire session tends to allocate (its proxies grabbed
  // 5101/5102 during development of this suite).
  webServer: [
    {
      command: 'dotnet ../src/Auth/Auth.Api/bin/Release/net10.0/Auth.Api.dll',
      env: {
        ASPNETCORE_URLS: 'http://127.0.0.1:5201',
        ASPNETCORE_ENVIRONMENT: 'Development',
        Jwt__SigningKey: JWT_KEY,
        // Every scenario signs in through the UI (per-tab sessionStorage); with 13+
        // scenarios the default 10 requests / 30 s per IP would start returning 429s.
        // Mirrors what tests/Bdd/Support/AspireStack.cs does for the spec environment;
        // the 429 shape itself is proven in-process by AuthRateLimitTests.
        RateLimiting__Auth__PermitLimit: '100',
      },
      url: 'http://127.0.0.1:5201/health',
      reuseExistingServer: !process.env.CI,
    },
    {
      command: 'dotnet ../src/Quotes/Quotes.Api/bin/Release/net10.0/Quotes.Api.dll',
      env: {
        ASPNETCORE_URLS: 'http://127.0.0.1:5202',
        ASPNETCORE_ENVIRONMENT: 'Development',
        Jwt__SigningKey: JWT_KEY,
      },
      url: 'http://127.0.0.1:5202/health',
      reuseExistingServer: !process.env.CI,
    },
    {
      // --host pins IPv4 loopback: Vite binds ::1 only by default, which leaves the
      // 127.0.0.1 readiness probe refused even though the server is up.
      command: 'pnpm run dev --host 127.0.0.1 --port 5173 --strictPort',
      env: {
        AUTH_API_HTTP: 'http://127.0.0.1:5201',
        QUOTES_API_HTTP: 'http://127.0.0.1:5202',
      },
      url: 'http://127.0.0.1:5173',
      reuseExistingServer: !process.env.CI,
    },
  ],
});
