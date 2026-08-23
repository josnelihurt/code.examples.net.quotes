import { expect } from '@playwright/test';
import { Given, When, Then, type QuoteWorld } from './fixtures';

/** Unique per run: the fingerprint guard rejects normalized repeats of anything already published. */
function uniqueQuoteText(): string {
  return `The browser suite publishes words that tick at ${Date.now()}.`;
}

async function openPublishForm(page: import('@playwright/test').Page) {
  await page.getByRole('link', { name: 'Publish' }).click();
  await expect(page.getByRole('heading', { name: 'Publish a quote' })).toBeVisible();
}

async function fillUniqueQuote(page: import('@playwright/test').Page, quoteWorld: QuoteWorld, author: string) {
  quoteWorld.publishedText = uniqueQuoteText();
  quoteWorld.publishedAuthor = author;
  await page.getByLabel('Text').fill(quoteWorld.publishedText);
  await page.getByLabel('Author').fill(author);
}

async function submitAndWaitForConfirmation(page: import('@playwright/test').Page) {
  await page.getByRole('button', { name: 'Publish quote' }).click();
  await expect(page.getByText('Published to the catalog.')).toBeVisible();
}

When('I fill the publish form with unique text attributed to {string}', async ({ page, quoteWorld }, author: string) => {
  await openPublishForm(page);
  await fillUniqueQuote(page, quoteWorld, author);
});

When('I fill the publish form with the text {string} attributed to {string}', async ({ page }, text: string, author: string) => {
  await openPublishForm(page);
  await page.getByLabel('Text').fill(text);
  await page.getByLabel('Author').fill(author);
});

Given('I have published a quote with unique text attributed to {string}', async ({ page, quoteWorld }, author: string) => {
  await openPublishForm(page);
  await fillUniqueQuote(page, quoteWorld, author);
  await submitAndWaitForConfirmation(page);
});

When('I refill the publish form with the same text ending in an exclamation mark', async ({ page, quoteWorld }) => {
  // The form is cleared after a successful publish; both fields must come back.
  await page.getByLabel('Text').fill(quoteWorld.publishedText.replace(/\.$/, '!'));
  await page.getByLabel('Author').fill(quoteWorld.publishedAuthor);
});

When('I submit the publish form', async ({ page }) => {
  await page.getByRole('button', { name: 'Publish quote' }).click();
});

Then('the published quote is confirmed', async ({ page }) => {
  await expect(page.getByText('Published to the catalog.')).toBeVisible();
});

Then('the catalog lists the quote I published', async ({ page, quoteWorld }) => {
  await expect(page.getByText(quoteWorld.publishedText)).toBeVisible();
});

Then('an alert explains the conflict', async ({ page }) => {
  await expect(page.getByRole('alert')).toContainText('(409)');
});

Then('an alert explains the missing write permission', async ({ page }) => {
  await expect(page.getByRole('alert')).toContainText('(403)');
});
