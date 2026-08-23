import { expect } from '@playwright/test';
import { When, Then } from './fixtures';

When('I open the catalog', async ({ page }) => {
  // Exact: the publish confirmation also links to the catalog as "Browse the catalog".
  await page.getByRole('link', { name: 'Browse', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Catalog' })).toBeVisible();
});

Then('the catalog shows page {int} of {int}', async ({ page }, pageNumber: number, totalPages: number) => {
  await expect(page.getByText(`Page ${pageNumber} of ${totalPages}`)).toBeVisible();
});

Then('the catalog lists the seeded quote by {string}', async ({ page }, author: string) => {
  await expect(page.getByText(`— ${author}`)).toBeVisible();
});

When('I move to the next page', async ({ page }) => {
  await page.getByRole('button', { name: 'Next page' }).click();
});

When('I move to the previous page', async ({ page }) => {
  await page.getByRole('button', { name: 'Previous page' }).click();
});

/** Steps through the catalog one page at a time until the pager reports the last one. */
When('I move to the last page', async ({ page }) => {
  const next = page.getByRole('button', { name: 'Next page' });
  while (await next.isEnabled()) {
    const current = (await page.getByText(/^Page \d+ of \d+/).textContent())!.match(/^Page (\d+) of (\d+)/)!;
    await next.click();
    await expect(page.getByText(`Page ${Number(current[1]) + 1} of ${current[2]}`)).toBeVisible();
  }
});

Then('the previous page control is disabled', async ({ page }) => {
  await expect(page.getByRole('button', { name: 'Previous page' })).toBeDisabled();
});

Then('the next page control is disabled', async ({ page }) => {
  await expect(page.getByRole('button', { name: 'Next page' })).toBeDisabled();
});

Then('the catalog was served by {string}', async ({ page }, version: string) => {
  await expect(page.getByText(`Served by: ${version}`)).toBeVisible();
});
