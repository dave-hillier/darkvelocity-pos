import { test, expect } from '@playwright/test';
import { BACKOFFICE_URL, loginToBackoffice } from './fixtures';

/**
 * Grains exercised:
 *   - Inventory (stock levels, adjust inventory)
 *   - Supplier (supplier list)
 *   - Delivery (deliveries page)
 *   - PurchaseDocument (purchase orders page)
 */
test.describe('Backoffice inventory and procurement', () => {
  test.beforeEach(async ({ page }) => {
    await loginToBackoffice(page);
  });

  test('view stock levels', async ({ page }) => {
    await page.goto(`${BACKOFFICE_URL}/inventory/stock`);
    await expect(page.getByRole('heading', { name: 'Stock Levels' })).toBeVisible();

    // Summary cards are present
    await expect(page.getByText('Total Items')).toBeVisible();
    await expect(page.getByText('Out of Stock')).toBeVisible();
    await expect(page.getByText('Low Stock')).toBeVisible();

    // Controls are present
    await expect(page.getByRole('button', { name: 'Start Stocktake' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Record Waste' })).toBeVisible();
  });

  test('view suppliers', async ({ page }) => {
    await page.goto(`${BACKOFFICE_URL}/procurement/suppliers`);
    await expect(page.getByRole('heading', { name: 'Suppliers' })).toBeVisible();

    await expect(page.locator('input[placeholder="Search suppliers..."]')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Add Supplier' })).toBeVisible();
  });

  test('view deliveries page', async ({ page }) => {
    await page.goto(`${BACKOFFICE_URL}/procurement/deliveries`);
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
  });

  test('view purchase orders page', async ({ page }) => {
    await page.goto(`${BACKOFFICE_URL}/procurement/purchase-orders`);
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
  });

  test('view ingredients page', async ({ page }) => {
    await page.goto(`${BACKOFFICE_URL}/inventory/ingredients`);
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
  });
});
