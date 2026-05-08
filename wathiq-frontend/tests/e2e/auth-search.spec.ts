import { expect, test } from '@playwright/test';

test.describe('Manager E2E journey', () => {
  test('manager can log in and search for a real document', async ({ page }) => {
    await page.goto('/login');

    await page.locator('#email').fill('search-manager@example.com');
    await page.locator('#password').fill('Pass123!');
    await page.getByRole('button', { name: 'Login' }).click();

    await expect(page).toHaveURL(/\/dashboard$/);
    await expect(page.getByText('Hello, Search Manager')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Search for a specific document' })).toBeVisible();

    await page.getByRole('button', { name: 'Search for a specific document' }).click();

    await expect(page).toHaveURL(/\/search$/);
    await page.getByPlaceholder('Search by title, reference, or issuing entity...').fill('Archive');
    await page.getByRole('button', { name: 'Search', exact: true }).click();

    await expect(page.getByRole('heading', { name: 'Archive Contract 2026' })).toBeVisible();
    await expect(page.getByText('ARCH-2026-001')).toBeVisible();
    await expect(page.getByText('Operations Directorate')).toBeVisible();
  });
});
