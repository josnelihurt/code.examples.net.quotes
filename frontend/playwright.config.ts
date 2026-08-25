import { readFileSync } from 'node:fs';
import { defineConfig, devices } from '@playwright/test';
import { defineBddConfig } from 'playwright-bdd';

const testDir = defineBddConfig({
  features: 'e2e/features/**/*.feature',
  steps: 'e2e/steps/**/*.ts',
});

// Shared by both APIs (Auth signs, Quotes verifies). Required: any value of at least 32
// characters — CI generates an ephemeral per-run key; locally export your own (see
// docs/dev-credentials.md). Failing fast here beats a cryptic JwtBearer error at boot.
const JWT_KEY = process.env.E2E_SIGNING_KEY ?? '';
if (!JWT_KEY || JWT_KEY.length < 32) {
  throw new Error(
    'E2E_SIGNING_KEY must be set to a value of at least 32 characters before running e2e ' +
      '(see docs/dev-credentials.md; CI generates an ephemeral one).',
  );
}

// scripts/e2e.env is the one copy of the throwaway catalog's connection values — the
// same file scripts/e2e.sh and the CI e2e job source when they start the container —
// parsed here so this config and the container can never drift apart. The environment
// wins over the file: that is how scripts/e2e.sh hands over its per-worktree ports and
// how any E2E_PG_* value can override a single run.
function parseE2eEnv(): Record<string, string> {
  return Object.fromEntries(
    readFileSync(new URL('../scripts/e2e.env', import.meta.url), 'utf8')
      .split('\n')
      .map((line) => line.trim())
      .filter((line) => line && !line.startsWith('#'))
      .map((line) => {
        const sep = line.indexOf('=');
        return [line.slice(0, sep), line.slice(sep + 1)] as [string, string];
      }),
  );
}
const e2eEnv = parseE2eEnv();
const e2eValue = (key: string) => process.env[key] ?? e2eEnv[key] ?? '';

// scripts/e2e.sh namespaces every port per worktree (several agents/worktrees can share
// one machine without deleting each other's containers or reusing each other's servers)
// and exports the E2E_* values. The API/Vite fallbacks are the historical fixed ports;
// the PostgreSQL fallback is scripts/e2e.env's value — the same file CI starts its
// container from — so standalone `pnpm run test:e2e` runs and CI keep agreeing with it.
const PG_PORT = e2eValue('E2E_PG_PORT');
const AUTH_PORT = process.env.E2E_AUTH_PORT ?? '5201';
const QUOTES_PORT = process.env.E2E_QUOTES_PORT ?? '5202';
const VITE_PORT = process.env.E2E_VITE_PORT ?? '5173';

const AUTH_HTTP = `http://127.0.0.1:${AUTH_PORT}`;
const QUOTES_HTTP = `http://127.0.0.1:${QUOTES_PORT}`;
const VITE_HTTP = `http://127.0.0.1:${VITE_PORT}`;

// The quotes catalog database scripts/e2e.sh (locally) / ci.yml (in CI) starts before the
// run, described end to end by scripts/e2e.env: fixed loopback port, throwaway
// credentials. The API migrates + seeds it at boot.
const QUOTES_DB =
  `Host=127.0.0.1;Port=${PG_PORT};` +
  `Username=${e2eValue('E2E_PG_USER')};Password=${e2eValue('E2E_PG_PASSWORD')};` +
  `Database=${e2eValue('E2E_PG_DATABASE')}`;

export default defineConfig({
  testDir,
  reporter: process.env.CI ? [['html', { open: 'never' }], ['list']] : 'html',
  use: { baseURL: VITE_HTTP, trace: 'on-first-retry' },
  projects: [{ name: 'chromium', use: devices['Desktop Chrome'] }],
  // The catalog lives in the throwaway PostgreSQL started before the run, shared by every
  // scenario of the run, and browsing scenarios assert exact page counts over the seeded
  // catalog — so scenarios must not interleave. One worker keeps publishing-quotes from
  // growing the catalog while browsing-quotes is asserting on it.
  workers: 1,
  // The topology the old CI smoke job already proved: both APIs on loopback ports plus
  // the Vite dev server, wired by env vars (vite.config.ts proxies /api/* to them).
  // The Aspire test host is deliberately not used here — its dynamic ports are invisible
  // to Node, and browser journeys need the real dev server anyway. Ports come from
  // scripts/e2e.sh per worktree (see the E2E_* block above); the fallbacks sit outside
  // the range a running Aspire session tends to allocate (its proxies grabbed 5101/5102
  // during development of this suite).
  webServer: [
    {
      command: 'dotnet ../src/Auth/Auth.Api/bin/Release/net10.0/Auth.Api.dll',
      env: {
        ASPNETCORE_URLS: AUTH_HTTP,
        ASPNETCORE_ENVIRONMENT: 'Development',
        Jwt__SigningKey: JWT_KEY,
        // Every scenario signs in through the UI (per-tab sessionStorage); with 13+
        // scenarios the default 10 requests / 30 s per IP would start returning 429s.
        // Mirrors what tests/Bdd/Support/AspireStack.cs does for the spec environment;
        // the 429 shape itself is proven in-process by AuthRateLimitTests.
        RateLimiting__Auth__PermitLimit: '100',
      },
      url: `${AUTH_HTTP}/health`,
      reuseExistingServer: !process.env.CI,
    },
    {
      command: 'dotnet ../src/Quotes/Quotes.Api/bin/Release/net10.0/Quotes.Api.dll',
      env: {
        ASPNETCORE_URLS: QUOTES_HTTP,
        ASPNETCORE_ENVIRONMENT: 'Development',
        Jwt__SigningKey: JWT_KEY,
        ConnectionStrings__quotesdb: QUOTES_DB,
      },
      url: `${QUOTES_HTTP}/health`,
      timeout: 120_000, // includes the at-boot migration + seed of the throwaway database
      reuseExistingServer: !process.env.CI,
    },
    {
      // --host pins IPv4 loopback: Vite binds ::1 only by default, which leaves the
      // 127.0.0.1 readiness probe refused even though the server is up.
      command: `pnpm run dev --host 127.0.0.1 --port ${VITE_PORT} --strictPort`,
      env: {
        AUTH_API_HTTP: AUTH_HTTP,
        QUOTES_API_HTTP: QUOTES_HTTP,
      },
      url: VITE_HTTP,
      reuseExistingServer: !process.env.CI,
    },
  ],
});
