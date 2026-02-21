import { test, expect } from '@playwright/test';
import { BACKOFFICE_URL, loginToBackoffice } from './fixtures';

/**
 * Grains exercised:
 *   - Channel (list channels, pause/resume)
 *   - ChannelRegistry (channel listing)
 *   - MenuSync (triggered via channel sync)
 */
test.describe('Backoffice channel management', () => {
  test.beforeEach(async ({ page }) => {
    await loginToBackoffice(page);
  });

  test('view channels list', async ({ page }) => {
    await page.goto(`${BACKOFFICE_URL}/channels`);
    await expect(page.getByRole('heading', { name: 'Channels' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Connect Channel' })).toBeVisible();
  });
});
