import { expect } from '@playwright/test';
import { Given, When, Then } from './fixtures';

Given('I am on the sign-in page', async ({ page }) => {
  await page.goto('/');
});

When('I visit {string}', async ({ page }, path: string) => {
  await page.goto(path);
});

When('I sign in as {string} with password {string}', async ({ page }, username: string, password: string) => {
  await page.getByLabel('Username').fill(username);
  await page.getByLabel('Password').fill(password);
  await page.getByRole('button', { name: 'Sign in' }).click();
});

When('I sign out', async ({ page }) => {
  await page.getByRole('button', { name: 'Sign out' }).click();
});

Then('I reach the quote page', async ({ page }) => {
  await expect(page.getByRole('heading', { name: 'Random quote' })).toBeVisible();
});

Then('I stay on the sign-in page', async ({ page }) => {
  await expect(page.getByRole('heading', { name: 'Sign in' })).toBeVisible();
});

Then('an alert explains the problem', async ({ page }) => {
  await expect(page.getByRole('alert')).toBeVisible();
});
