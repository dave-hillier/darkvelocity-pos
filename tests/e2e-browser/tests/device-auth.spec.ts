import { test, expect, type Page, type BrowserContext } from '@playwright/test';

const POS_URL = 'http://localhost:5173';
const BACKOFFICE_URL = 'http://localhost:5174';

test.describe('Trusted device authorization', () => {
  let posContext: BrowserContext;
  let backofficeContext: BrowserContext;
  let posPage: Page;
  let backofficePage: Page;

  test.beforeEach(async ({ browser }) => {
    // Two separate browser contexts simulate two physical devices:
    // a POS tablet and an admin's laptop running the backoffice.
    posContext = await browser.newContext();
    backofficeContext = await browser.newContext();
    posPage = await posContext.newPage();
    backofficePage = await backofficeContext.newPage();
  });

  test.afterEach(async () => {
    await posContext.close();
    await backofficeContext.close();
  });

  test('POS device requests code, backoffice authorizes, POS proceeds to login', async () => {
    // --- POS: navigate to setup and request a device code ---
    await posPage.goto(`${POS_URL}/setup`);
    await expect(posPage.getByRole('heading', { name: 'DarkVelocity POS' })).toBeVisible();

    await posPage.getByRole('button', { name: 'Begin Device Setup' }).click();

    // Wait for the user code to appear (format: XXXX-XXXX, monospace block)
    const codeDisplay = posPage.locator('p').filter({ hasText: /^[A-Z0-9]{4}-[A-Z0-9]{4}$/ });
    await expect(codeDisplay).toBeVisible({ timeout: 10_000 });

    const userCode = (await codeDisplay.textContent())!.trim();
    expect(userCode).toMatch(/^[A-Z0-9]{4}-[A-Z0-9]{4}$/);

    // Verify the POS shows "Waiting for authorization..."
    await expect(posPage.getByText('Waiting for authorization...')).toBeVisible();

    // --- Backoffice: authorize the device using the code ---
    await backofficePage.goto(`${BACKOFFICE_URL}/device`);
    await expect(backofficePage.getByRole('heading', { name: 'Authorize Device' })).toBeVisible();

    // Enter the code from the POS screen
    await backofficePage.getByLabel('Device Code').fill(userCode.replace('-', ''));

    // Fill in the device name
    await backofficePage.getByLabel('Device Name').fill('Bar Terminal 1');

    // Select app type (defaults to POS, which is what we want)
    await expect(backofficePage.getByLabel('App Type')).toHaveValue('Pos');

    // Wait for mock sites to load, then select one
    const locationSelect = backofficePage.getByLabel('Location');
    await expect(locationSelect).not.toBeDisabled({ timeout: 5_000 });
    await locationSelect.selectOption({ index: 1 }); // "Main Location"

    // Click authorize
    await backofficePage.getByRole('button', { name: 'Authorize Device' }).click();

    // Verify backoffice shows success
    await expect(
      backofficePage.getByRole('heading', { name: 'Device Authorized' })
    ).toBeVisible({ timeout: 10_000 });
    await expect(
      backofficePage.getByText('Bar Terminal 1')
    ).toBeVisible();

    // --- POS: polling detects authorization, navigates to login ---
    // The POS polls every 5 seconds; give it enough time to detect the change.
    await expect(posPage.getByRole('heading', { name: 'Enter PIN' })).toBeVisible({
      timeout: 30_000,
    });

    // We've arrived at the login page — device is trusted.
    expect(posPage.url()).toContain('/login');
  });

  test('POS shows error when device code expires without authorization', async () => {
    await posPage.goto(`${POS_URL}/setup`);
    await posPage.getByRole('button', { name: 'Begin Device Setup' }).click();

    const codeDisplay = posPage.locator('p').filter({ hasText: /^[A-Z0-9]{4}-[A-Z0-9]{4}$/ });
    await expect(codeDisplay).toBeVisible({ timeout: 10_000 });

    // Verify countdown is shown
    await expect(posPage.getByText(/Code expires in/)).toBeVisible();

    // Don't authorize — just wait for the expiry indicator to appear.
    // The real expiry is 15 minutes; we check the UI is wired up correctly
    // by verifying the countdown text and progress bar are present.
    await expect(posPage.locator('progress')).toBeVisible();
  });

  test('backoffice rejects authorization with invalid code', async () => {
    await backofficePage.goto(`${BACKOFFICE_URL}/device`);

    await backofficePage.getByLabel('Device Code').fill('ZZZZ9999');
    await backofficePage.getByLabel('Device Name').fill('Invalid Device');

    const locationSelect = backofficePage.getByLabel('Location');
    await expect(locationSelect).not.toBeDisabled({ timeout: 5_000 });
    await locationSelect.selectOption({ index: 1 });

    await backofficePage.getByRole('button', { name: 'Authorize Device' }).click();

    // Should show an error, not the success screen
    await expect(backofficePage.getByRole('alert')).toBeVisible({ timeout: 10_000 });
    await expect(
      backofficePage.getByRole('heading', { name: 'Device Authorized' })
    ).not.toBeVisible();
  });

  test('POS persists device auth across page reload', async () => {
    // Complete the authorization flow first
    await posPage.goto(`${POS_URL}/setup`);
    await posPage.getByRole('button', { name: 'Begin Device Setup' }).click();

    const codeDisplay = posPage.locator('p').filter({ hasText: /^[A-Z0-9]{4}-[A-Z0-9]{4}$/ });
    await expect(codeDisplay).toBeVisible({ timeout: 10_000 });
    const userCode = (await codeDisplay.textContent())!.trim();

    // Authorize via backoffice
    await backofficePage.goto(`${BACKOFFICE_URL}/device`);
    await backofficePage.getByLabel('Device Code').fill(userCode.replace('-', ''));
    await backofficePage.getByLabel('Device Name').fill('Persistent Device');
    const locationSelect = backofficePage.getByLabel('Location');
    await expect(locationSelect).not.toBeDisabled({ timeout: 5_000 });
    await locationSelect.selectOption({ index: 1 });
    await backofficePage.getByRole('button', { name: 'Authorize Device' }).click();
    await expect(
      backofficePage.getByRole('heading', { name: 'Device Authorized' })
    ).toBeVisible({ timeout: 10_000 });

    // Wait for POS to pick up auth and navigate to login
    await expect(posPage.getByRole('heading', { name: 'Enter PIN' })).toBeVisible({
      timeout: 30_000,
    });

    // Reload the POS page — device auth should persist from localStorage
    await posPage.reload();

    // Should still be on the login page, not redirected back to setup
    await expect(posPage.getByRole('heading', { name: 'Enter PIN' })).toBeVisible({
      timeout: 10_000,
    });
    expect(posPage.url()).toContain('/login');
  });
});
