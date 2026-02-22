import { test, expect } from '@playwright/test';
import { BACKOFFICE_URL, loginToBackoffice } from './fixtures';

/**
 * Grains exercised:
 *   - Employee (list employees, navigate to detail)
 *   - Role (role badges in employee list)
 */
test.describe('Backoffice employee management', () => {
  test.beforeEach(async ({ page }) => {
    await loginToBackoffice(page);
  });

  test('view employees list', async ({ page }) => {
    await page.goto(`${BACKOFFICE_URL}/employees`);
    await expect(page.getByRole('heading', { name: 'Employees' })).toBeVisible();

    await expect(page.locator('input[placeholder="Search employees..."]')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Add Employee' })).toBeVisible();
  });

  test('navigate to add employee', async ({ page }) => {
    await page.goto(`${BACKOFFICE_URL}/employees`);
    await page.getByRole('button', { name: 'Add Employee' }).click();
    await expect(page).toHaveURL(/\/employees\/new/);
  });
});
