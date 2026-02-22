import { defineConfig, devices } from '@playwright/test';

const API_PORT = 5200;
const POS_PORT = 5173;
const BACKOFFICE_PORT = 5174;
const KDS_PORT = 5175;

export default defineConfig({
  testDir: './tests',
  fullyParallel: false, // Device auth tests coordinate across two browser contexts
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  workers: 1,
  reporter: process.env.CI ? 'github' : 'html',
  timeout: 60_000,

  use: {
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
  },

  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],

  webServer: [
    {
      command: 'dotnet run --project src/DarkVelocity.Host',
      cwd: '../../',
      port: API_PORT,
      reuseExistingServer: !process.env.CI,
      timeout: 120_000,
    },
    {
      command: `npx vite --port ${POS_PORT}`,
      cwd: '../../apps/pos',
      port: POS_PORT,
      reuseExistingServer: !process.env.CI,
      env: {
        VITE_API_URL: `http://localhost:${API_PORT}`,
      },
    },
    {
      command: `npx vite --port ${BACKOFFICE_PORT}`,
      cwd: '../../apps/backoffice',
      port: BACKOFFICE_PORT,
      reuseExistingServer: !process.env.CI,
      env: {
        VITE_API_URL: `http://localhost:${API_PORT}`,
      },
    },
    {
      command: `npx vite --port ${KDS_PORT}`,
      cwd: '../../apps/kds',
      port: KDS_PORT,
      reuseExistingServer: !process.env.CI,
      env: {
        VITE_API_URL: `http://localhost:${API_PORT}`,
      },
    },
  ],
});
