import { test as base, createBdd } from 'playwright-bdd';

// The Playwright test object re-exported by playwright-bdd (it brands the runner so
// generated tests can bind), plus the step decorators. Scenarios sign in through the
// real UI every time: the app keeps its session in sessionStorage (per-tab), and signing
// in is itself one of the flows under test — with only two pages the cost is negligible.
export const test = base;

export const { Given, When, Then } = createBdd(test);
