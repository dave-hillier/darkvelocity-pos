import { test, expect } from '@playwright/test';
import { BACKOFFICE_URL, loginToBackoffice } from './fixtures';

/**
 * Grains exercised:
 *   - RecipeDocument (recipe CMS)
 *   - RecipeRegistry (recipe listing)
 *   - IngredientPrice (ingredient costs via recipes)
 */
test.describe('Backoffice recipe management', () => {
  test.beforeEach(async ({ page }) => {
    await loginToBackoffice(page);
  });

  test('view recipes page', async ({ page }) => {
    await page.goto(`${BACKOFFICE_URL}/menu/recipes`);
    await expect(page.getByRole('heading', { name: /Recipes/ })).toBeVisible();
  });
});
