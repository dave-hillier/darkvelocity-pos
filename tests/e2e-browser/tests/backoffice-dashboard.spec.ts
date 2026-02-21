import { test, expect } from '@playwright/test';
import { BACKOFFICE_URL, loginToBackoffice } from './fixtures';

/**
 * Grains exercised:
 *   - SiteDashboard (dashboard KPIs)
 *   - DailySales (today's sales summary)
 *   - CostAlert (cost alerts count)
 *   - MenuEngineering (margin analysis report)
 */
test.describe('Backoffice dashboard and reports', () => {
  test.beforeEach(async ({ page }) => {
    await loginToBackoffice(page);
  });

  test('dashboard shows summary cards', async ({ page }) => {
    // loginToBackoffice already navigates to /dashboard
    await expect(page.getByRole('heading', { name: 'Dashboard' })).toBeVisible();

    // Four summary cards should be present
    await expect(page.getByText("Today's Sales")).toBeVisible();
    await expect(page.getByText('Orders')).toBeVisible();
    await expect(page.getByText('Gross Margin')).toBeVisible();
    await expect(page.getByText('Cost Alerts')).toBeVisible();
  });

  test('dashboard quick actions navigate correctly', async ({ page }) => {
    await expect(page.getByRole('heading', { name: 'Quick Actions' })).toBeVisible();

    await page.getByRole('button', { name: 'View Margins' }).click();
    await expect(page).toHaveURL(/\/reports\/margins/);
  });

  test('reports page shows available reports', async ({ page }) => {
    await page.goto(`${BACKOFFICE_URL}/reports`);
    await expect(page.getByRole('heading', { name: 'Reports' })).toBeVisible();

    // Report cards are present
    await expect(page.getByText('Margin Analysis')).toBeVisible();
    await expect(page.getByText('Daily Sales & COGS')).toBeVisible();
    await expect(page.getByText('Category Analysis')).toBeVisible();
  });

  test('navigate to margin analysis report', async ({ page }) => {
    await page.goto(`${BACKOFFICE_URL}/reports`);
    const marginCard = page.locator('article').filter({ hasText: 'Margin Analysis' });
    await marginCard.getByRole('button', { name: 'View Report' }).click();
    await expect(page).toHaveURL(/\/reports\/margins/);
  });
});
