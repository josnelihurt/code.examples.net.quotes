import { test as base, createBdd } from 'playwright-bdd';

// The Playwright test object re-exported by playwright-bdd (it brands the runner so
// generated tests can bind), plus the step decorators. Scenarios sign in through the
// real UI every time: the app keeps its session in sessionStorage (per-tab), and signing
// in is itself one of the flows under test — the pages are small enough that the cost
// is negligible.

// Per-scenario scratchpad: steps record what a scenario published (text and author)
// so later steps in the same scenario can republish or find it again.
export interface QuoteWorld {
  publishedText: string;
  publishedAuthor: string;
}

export const test = base.extend<{ quoteWorld: QuoteWorld }>({
  quoteWorld: [{ publishedText: '', publishedAuthor: '' }, { scope: 'test' }],
});

export const { Given, When, Then } = createBdd(test);
