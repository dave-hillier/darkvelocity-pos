import { test, expect, type Page, type BrowserContext } from '@playwright/test';
import { KDS_URL, BACKOFFICE_URL } from './fixtures';

const KDS_PORT = 5175;

/**
 * Grains exercised:
 *   - DeviceAuth (KDS device authorization)
 *   - Device (KDS device registration)
 *   - KitchenStation (station selection)
 *   - KitchenTicket (ticket display on KDS)
 */
test.describe('KDS kitchen display', () => {
  test('KDS device setup page loads', async ({ page }) => {
    await page.goto(`${KDS_URL}/setup`);
    await expect(page.getByRole('heading', { name: 'DarkVelocity KDS' })).toBeVisible();
    await expect(page.getByText('Kitchen Display System')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Begin Device Setup' })).toBeVisible();
  });

  test('KDS device requests code and shows authorization UI', async ({ page }) => {
    await page.goto(`${KDS_URL}/setup`);
    await page.getByRole('button', { name: 'Begin Device Setup' }).click();

    // Should show the authorization screen with a code
    await expect(page.getByRole('heading', { name: 'Authorize Kitchen Display' })).toBeVisible({
      timeout: 10_000,
    });

    const codeDisplay = page.locator('p').filter({ hasText: /^[A-Z0-9]{4}-[A-Z0-9]{4}$/ });
    await expect(codeDisplay).toBeVisible();

    // Countdown and progress bar visible
    await expect(page.getByText(/Code expires in/)).toBeVisible();
    await expect(page.locator('progress')).toBeVisible();
    await expect(page.getByText('Waiting for authorization...')).toBeVisible();
  });

  test('KDS station select page with injected auth', async ({ page }) => {
    // Inject device auth to skip the device code flow
    await page.goto(KDS_URL);
    await page.evaluate(() => {
      localStorage.setItem(
        'darkvelocity_kds_device',
        JSON.stringify({
          deviceToken: 'test-kds-token',
          deviceId: '00000000-0000-0000-0000-000000000077',
          organizationId: '00000000-0000-0000-0000-000000000001',
          siteId: '00000000-0000-0000-0000-000000000001',
          deviceName: 'Test KDS',
        }),
      );
    });
    await page.goto(`${KDS_URL}/station`);

    // Station selection page should load
    await expect(page.getByRole('heading', { name: 'Select Station' })).toBeVisible({
      timeout: 10_000,
    });
    await expect(
      page.getByText('Choose which kitchen station this display will show orders for'),
    ).toBeVisible();

    // Refresh and Reset buttons should be present
    await expect(page.getByRole('button', { name: 'Refresh' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Reset Device' })).toBeVisible();
  });

  test('KDS display page with injected auth and station', async ({ page }) => {
    // Inject both device auth and station selection
    await page.goto(KDS_URL);
    await page.evaluate(() => {
      localStorage.setItem(
        'darkvelocity_kds_device',
        JSON.stringify({
          deviceToken: 'test-kds-token',
          deviceId: '00000000-0000-0000-0000-000000000077',
          organizationId: '00000000-0000-0000-0000-000000000001',
          siteId: '00000000-0000-0000-0000-000000000001',
          deviceName: 'Test KDS',
        }),
      );
      localStorage.setItem(
        'darkvelocity_kds_station',
        JSON.stringify({
          stationId: '00000000-0000-0000-0000-000000000055',
          stationName: 'Grill Station',
        }),
      );
    });
    await page.goto(`${KDS_URL}/display`);

    // Kitchen display header shows station name and time
    await expect(page.getByText('Grill Station')).toBeVisible({ timeout: 10_000 });
    await expect(page.getByRole('button', { name: 'Change Station' })).toBeVisible();

    // Legend at the bottom
    await expect(page.getByText('Pending')).toBeVisible();
    await expect(page.getByText('Cooking')).toBeVisible();
    await expect(page.getByText('Ready')).toBeVisible();
  });
});
