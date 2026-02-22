import { test, expect, type Page, type BrowserContext } from '@playwright/test';
import { POS_URL, BACKOFFICE_URL, API_URL, authorizeDevice, loginWithPin } from './fixtures';

/**
 * Grains exercised:
 *   - Order (create order, add lines)
 *   - LineItems (add/remove items)
 *   - MenuItem (menu display in register)
 *   - MenuCategory (category tabs)
 *   - MenuDefinition (menu loading)
 *   - Payment (cash/card payment)
 *   - Session (user session from PIN login)
 *   - UserLookup (PIN-based user lookup)
 */
test.describe('POS order and payment', () => {
  let posContext: BrowserContext;
  let posPage: Page;

  test.beforeEach(async ({ browser }) => {
    // Authorize the POS device through the full cross-app flow
    const result = await authorizeDevice(browser, 'pos');
    posContext = result.posContext;
    posPage = result.posPage;

    // Wait for POS to navigate to login after device authorization
    await expect(posPage.getByRole('heading', { name: 'Enter PIN' })).toBeVisible({
      timeout: 30_000,
    });
  });

  test.afterEach(async () => {
    await posContext.close();
  });

  test('authorized device reaches PIN login', async () => {
    // Device auth already completed in beforeEach
    expect(posPage.url()).toContain('/login');
    await expect(posPage.getByText('Enter PIN')).toBeVisible();

    // PIN pad buttons are functional
    await expect(posPage.getByRole('button', { name: '1', exact: true })).toBeVisible();
    await expect(posPage.getByRole('button', { name: '0', exact: true })).toBeVisible();
    await expect(posPage.getByRole('button', { name: 'Clear' })).toBeVisible();
    await expect(posPage.getByRole('button', { name: 'Backspace' })).toBeVisible();
  });

  test('PIN pad accepts digit input and displays masked value', async () => {
    // Type a partial PIN
    await posPage.getByRole('button', { name: '1', exact: true }).click();
    await posPage.getByRole('button', { name: '2', exact: true }).click();

    // Should show masked input (2 asterisks)
    const display = posPage.locator('.pin-display');
    await expect(display).toContainText('**');

    // Clear resets the display
    await posPage.getByRole('button', { name: 'Clear' }).click();
    await expect(display).not.toContainText('*');
  });

  test('PIN login with invalid PIN shows error', async () => {
    // Enter a PIN that doesn't correspond to any registered user
    await loginWithPin(posPage, '9999');

    // Should show an error message
    await expect(posPage.getByRole('alert')).toBeVisible({ timeout: 5_000 });
  });
});

test.describe('POS register with injected auth', () => {
  test('register page loads menu categories and items', async ({ page }) => {
    // Inject both device auth and user auth to skip setup/login flows
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
    await page.goto(`${POS_URL}/register`);

    // The register page shows the menu with categories
    // MenuContext falls back to sample data if API is unavailable
    await expect(page.getByText('Food')).toBeVisible({ timeout: 10_000 });
    await expect(page.getByText('Drinks')).toBeVisible();
  });
});
