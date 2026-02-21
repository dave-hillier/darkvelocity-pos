import { test, expect } from '@playwright/test';
import { POS_URL } from './fixtures';

/**
 * Grains exercised:
 *   - Table (table status display)
 *   - Booking (today's bookings view)
 *   - BookingCalendar (day view query)
 *   - CustomerVisitHistory (repeat guest indicators)
 */
test.describe('POS tables and bookings view', () => {
  test.beforeEach(async ({ page }) => {
    // Inject device + user auth to reach the tables page
    await page.goto(POS_URL);
    await page.evaluate(() => {
      localStorage.setItem(
        'darkvelocity_device',
        JSON.stringify({
          deviceToken: 'test-device-token',
          deviceId: '00000000-0000-0000-0000-000000000099',
          organizationId: '00000000-0000-0000-0000-000000000001',
          siteId: '00000000-0000-0000-0000-000000000001',
          deviceName: 'Test POS',
        }),
      );
      localStorage.setItem(
        'darkvelocity_auth',
        JSON.stringify({
          accessToken: 'test-user-token',
          refreshToken: 'test-refresh-token',
          sessionId: '00000000-0000-0000-0000-000000000088',
          userId: '00000000-0000-0000-0000-000000000001',
          displayName: 'Test User',
        }),
      );
    });
  });

  test('tables page shows bookings for today', async ({ page }) => {
    await page.goto(`${POS_URL}/tables`);

    // The tables page shows today's bookings
    await expect(page.getByText("Today's Bookings")).toBeVisible({ timeout: 10_000 });

    // Back button returns to register
    await expect(page.getByRole('button', { name: 'Back' })).toBeVisible();
  });

  test('tables page back button returns to register', async ({ page }) => {
    await page.goto(`${POS_URL}/tables`);
    await expect(page.getByText("Today's Bookings")).toBeVisible({ timeout: 10_000 });

    await page.getByRole('button', { name: 'Back' }).click();
    await expect(page).toHaveURL(/\/register/);
  });
});
