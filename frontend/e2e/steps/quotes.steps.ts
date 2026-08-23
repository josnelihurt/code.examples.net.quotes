import { expect } from '@playwright/test';
import { When, Then } from './fixtures';

When('I fetch a random quote', async ({ page }) => {
  await page.getByRole('button', { name: 'Get random quote' }).click();
});

When('I switch the API version to {string}', async ({ page }, version: string) => {
  await page.check(`#version-${version}`);
});

Then('a quote is displayed', async ({ page }) => {
  await expect(page.locator('blockquote.quote')).toBeVisible();
});

Then('the quote was served by {string}', async ({ page }, version: string) => {
  await expect(page.getByText(`Served by: ${version}`)).toBeVisible();
});
