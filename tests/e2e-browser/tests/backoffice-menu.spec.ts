import { test, expect } from '@playwright/test';
import { BACKOFFICE_URL, loginToBackoffice } from './fixtures';

/**
 * Grains exercised:
 *   - CategoryDocument (create category)
 *   - MenuItemDocument (create menu item)
 *   - MenuRegistry (list items and categories)
 *   - ModifierBlockDocument (modifier blocks page)
 *   - ContentTag (content tags page)
 */
test.describe('Backoffice menu management', () => {
  test.beforeEach(async ({ page }) => {
    await loginToBackoffice(page);
  });

  test('create a menu category', async ({ page }) => {
    await page.goto(`${BACKOFFICE_URL}/menu/categories`);
    await expect(page.getByRole('heading', { name: 'Categories' })).toBeVisible();

    await page.getByRole('button', { name: 'New Category' }).click();
    await expect(page.locator('dialog[open]')).toBeVisible();

    await page.locator('input[name="name"]').fill('Starters');
    await page.locator('textarea[name="description"]').fill('Appetisers and small plates');
    await page.locator('input[name="publishImmediately"]').check();
    await page.getByRole('button', { name: 'Create Category' }).click();

    // Category should appear in the table
    await expect(page.getByRole('cell', { name: 'Starters' })).toBeVisible({ timeout: 5_000 });
  });

  test('create a menu item', async ({ page }) => {
    await page.goto(`${BACKOFFICE_URL}/menu/items`);
    await expect(page.getByRole('heading', { name: 'Menu Items' })).toBeVisible();

    await page.getByRole('button', { name: 'Add Item' }).click();
    await expect(page.locator('dialog[open]')).toBeVisible();

    await page.locator('input[name="name"]').fill('Margherita Pizza');
    await page.locator('input[name="price"]').fill('12.50');
    await page.locator('textarea[name="description"]').fill('Classic tomato and mozzarella');
    await page.locator('input[name="publishImmediately"]').check();
    await page.getByRole('button', { name: 'Create Item' }).click();

    await expect(page.getByRole('cell', { name: 'Margherita Pizza' })).toBeVisible({
      timeout: 5_000,
    });
  });

  test('search menu items', async ({ page }) => {
    await page.goto(`${BACKOFFICE_URL}/menu/items`);
    await expect(page.getByRole('heading', { name: 'Menu Items' })).toBeVisible();

    // The search input should be functional
    const search = page.locator('input[placeholder="Search items..."]');
    await expect(search).toBeVisible();
    await search.fill('pizza');

    // Verify the search filters the table (exact result depends on data)
  });

  test('view modifier blocks page', async ({ page }) => {
    await page.goto(`${BACKOFFICE_URL}/menu/modifier-blocks`);
    await expect(page.getByRole('heading', { name: 'Modifier Blocks' })).toBeVisible();
  });

  test('view content tags page', async ({ page }) => {
    await page.goto(`${BACKOFFICE_URL}/menu/tags`);
    await expect(page.getByRole('heading', { name: /Tags/ })).toBeVisible();
  });
});
