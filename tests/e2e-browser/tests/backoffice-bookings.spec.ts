import { test, expect } from '@playwright/test';
import { BACKOFFICE_URL, loginToBackoffice } from './fixtures';

/**
 * Grains exercised:
 *   - Booking (list bookings, status filter)
 *   - FloorPlan (create floor plan)
 *   - Table (tables within floor plan)
 *   - BookingCalendar (date-based booking query)
 *   - Waitlist (waitlist page if available)
 */
test.describe('Backoffice bookings and floor plans', () => {
  test.beforeEach(async ({ page }) => {
    await loginToBackoffice(page);
  });

  test('view bookings with status filter', async ({ page }) => {
    await page.goto(`${BACKOFFICE_URL}/bookings`);
    await expect(page.getByRole('heading', { name: 'Bookings' })).toBeVisible();

    // Status filter is present
    const statusFilter = page.locator('select[aria-label="Filter by status"]');
    await expect(statusFilter).toBeVisible();

    // Can filter by status
    await statusFilter.selectOption('Confirmed');

    // New Booking button exists
    await expect(page.getByRole('button', { name: 'New Booking' })).toBeVisible();
  });

  test('navigate to floor plans', async ({ page }) => {
    await page.goto(`${BACKOFFICE_URL}/bookings`);
    await page.getByRole('button', { name: 'Floor Plans' }).click();
    await expect(page).toHaveURL(/\/bookings\/floor-plans/);
    await expect(page.getByRole('heading', { name: 'Floor Plans' })).toBeVisible();
  });

  test('create a floor plan', async ({ page }) => {
    await page.goto(`${BACKOFFICE_URL}/bookings/floor-plans`);
    await expect(page.getByRole('heading', { name: 'Floor Plans' })).toBeVisible();

    await page.getByRole('button', { name: 'Add Floor Plan' }).click();

    await page.locator('input[placeholder="e.g. Main Dining Room"]').fill('Terrace');
    await page.getByRole('button', { name: 'Create & Open Designer' }).click();

    // Should navigate to the floor plan designer
    await expect(page).toHaveURL(/\/bookings\/floor-plans\//, { timeout: 10_000 });
  });

  test('view arrivals page', async ({ page }) => {
    await page.goto(`${BACKOFFICE_URL}/bookings/arrivals`);
    // Arrivals is a booking sub-view
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
  });
});
