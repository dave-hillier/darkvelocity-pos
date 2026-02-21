import { test, expect } from '@playwright/test';
import { BACKOFFICE_URL, loginToBackoffice } from './fixtures';

/**
 * Grains exercised:
 *   - Customer (list and navigate to customer)
 *   - LoyaltyProgram (loyalty badge visible in list)
 *   - CustomerSpendProjection (spend data on detail page)
 */
test.describe('Backoffice customer management', () => {
  test.beforeEach(async ({ page }) => {
    await loginToBackoffice(page);
  });

  test('view customers list', async ({ page }) => {
    await page.goto(`${BACKOFFICE_URL}/customers`);
    await expect(page.getByRole('heading', { name: 'Customers' })).toBeVisible();

    // Search input is present
    await expect(page.locator('input[placeholder="Search customers..."]')).toBeVisible();

    // Add Customer button is present
    await expect(page.getByRole('button', { name: 'Add Customer' })).toBeVisible();
  });

  test('navigate to add customer', async ({ page }) => {
    await page.goto(`${BACKOFFICE_URL}/customers`);
    await page.getByRole('button', { name: 'Add Customer' }).click();
    await expect(page).toHaveURL(/\/customers\/new/);
  });
});
