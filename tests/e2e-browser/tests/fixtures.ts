import { type Page, type Browser, type BrowserContext, expect } from '@playwright/test';

const POS_URL = 'http://localhost:5173';
const BACKOFFICE_URL = 'http://localhost:5174';
const KDS_URL = 'http://localhost:5175';
const API_URL = 'http://localhost:5200';

export { POS_URL, BACKOFFICE_URL, KDS_URL, API_URL };

/**
 * Log into the backoffice via the dev-login OAuth flow.
 * Navigates to /login, clicks "Dev Login", and waits for the redirect to /dashboard.
 */
export async function loginToBackoffice(page: Page): Promise<void> {
  await page.goto(`${BACKOFFICE_URL}/login`);
  await page.getByRole('button', { name: 'Dev Login' }).click();
  await page.waitForURL('**/dashboard', { timeout: 15_000 });
}

/**
 * Run the full device auth flow across two browser contexts.
 * Returns the user code for verification purposes.
 */
export async function authorizeDevice(
  browser: Browser,
  app: 'pos' | 'kds',
): Promise<{ posContext: BrowserContext; posPage: Page; userCode: string }> {
  const appUrl = app === 'pos' ? POS_URL : KDS_URL;
  const posContext = await browser.newContext();
  const backofficeContext = await browser.newContext();
  const posPage = await posContext.newPage();
  const backofficePage = await backofficeContext.newPage();

  // Request device code
  await posPage.goto(`${appUrl}/setup`);
  await posPage.getByRole('button', { name: 'Begin Device Setup' }).click();

  const codeDisplay = posPage.locator('p').filter({ hasText: /^[A-Z0-9]{4}-[A-Z0-9]{4}$/ });
  await expect(codeDisplay).toBeVisible({ timeout: 10_000 });
  const userCode = (await codeDisplay.textContent())!.trim();

  // Authorize via backoffice
  await backofficePage.goto(`${BACKOFFICE_URL}/device`);
  await backofficePage.getByLabel('Device Code').fill(userCode.replace('-', ''));
  await backofficePage.getByLabel('Device Name').fill(`Test ${app.toUpperCase()}`);
  const locationSelect = backofficePage.getByLabel('Location');
  await expect(locationSelect).not.toBeDisabled({ timeout: 5_000 });
  await locationSelect.selectOption({ index: 1 });
  await backofficePage.getByRole('button', { name: 'Authorize Device' }).click();
  await expect(
    backofficePage.getByRole('heading', { name: 'Device Authorized' }),
  ).toBeVisible({ timeout: 10_000 });

  await backofficeContext.close();
  return { posContext, posPage, userCode };
}

/**
 * Inject device auth state directly into localStorage to skip the device auth flow.
 * Useful when the test focus is on a later step (e.g. PIN login, order placement).
 */
export async function injectDeviceAuth(page: Page, appUrl: string): Promise<void> {
  await page.goto(appUrl);
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
  });
  await page.reload();
}

/**
 * Register a test user with a known PIN via the API, then log in on the POS.
 * Requires device auth to be set up first.
 */
export async function loginWithPin(page: Page, pin = '1234'): Promise<void> {
  for (const digit of pin) {
    await page.getByRole('button', { name: digit, exact: true }).click();
  }
  // PIN auto-submits when all 4 digits are entered
}
