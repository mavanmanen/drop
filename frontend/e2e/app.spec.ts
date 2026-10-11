import { test, expect } from '@playwright/test';

test.describe('Drop Web App UI E2E Tests', () => {
  test.beforeEach(async ({ page }) => {
    // Mock the backend API endpoints so E2E test runs fully isolated and fast
    await page.route('/api/auth/status', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ isAuthenticated: false, username: null }),
      });
    });

    await page.route('/api/clips*', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: [
            {
              id: 'e2eclip1',
              title: 'Playwright Victory Royale',
              description: 'Clutch win in finals',
              originalFileName: 'victory.mp4',
              fileSizeBytes: 2048000,
              contentType: 'video/mp4',
              durationSeconds: 42,
              hasThumbnail: false,
              videoUrl: '/api/clips/e2eclip1/stream',
              thumbnailUrl: '/api/clips/e2eclip1/thumbnail',
              createdAtUtc: new Date().toISOString(),
              viewsCount: 128,
              visibility: 'public',
              collections: [],
            },
          ],
          totalCount: 1,
          page: 1,
          pageSize: 24,
          totalPages: 1,
        }),
      });
    });

    await page.route('/api/collections', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          {
            id: 'col-e2e',
            name: 'Tournament Plays',
            description: null,
            createdAtUtc: new Date().toISOString(),
            clipCount: 1,
            coverThumbnailUrl: null,
            visibility: 'public',
            clips: [],
          },
        ]),
      });
    });
  });

  test('homepage displays header, brand title, and clip cards', async ({ page }) => {
    await page.goto('/');

    // Verify Brand
    await expect(page.locator('.brand-text')).toHaveText('drop');
    await expect(page.locator('.brand-tag')).toHaveText('clips');

    // Verify Nav Links
    await expect(page.locator('a.nav-link', { hasText: 'Clips' })).toBeVisible();
    await expect(page.locator('a.nav-link', { hasText: 'Collections' })).toBeVisible();

    // Verify Clip title rendered
    await expect(page.locator('text=Playwright Victory Royale')).toBeVisible();
  });

  test('admin login modal opens and validates inputs', async ({ page }) => {
    await page.goto('/');

    // Click Admin Login
    const loginButton = page.locator('button.btn-login');
    await expect(loginButton).toBeVisible();
    await loginButton.click();

    // Modal should be visible
    await expect(page.locator('.modal-card h2')).toHaveText('Admin Login');

    // Try submitting empty credentials
    await page.locator('button[type="submit"]', { hasText: 'Sign In' }).click();
    await expect(page.locator('.error-banner')).toContainText('Please enter both username and password');

    // Close modal
    await page.locator('.close-btn').click();
    await expect(page.locator('.modal-backdrop')).toHaveCount(0);
  });

  test('admin login succeeds and displays upload button and admin chip', async ({ page }) => {
    // Intercept login endpoint
    await page.route('/api/auth/login', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          token: 'mock-jwt-token',
          username: 'admin',
          isAuthenticated: true,
        }),
      });
    });

    await page.goto('/');

    await page.locator('button.btn-login').click();
    await page.locator('#admin-user').fill('admin');
    await page.locator('#admin-pass').fill('adminpassword');
    await page.locator('button[type="submit"]').click();

    // After login, upload button and admin username chip should appear
    await expect(page.locator('.btn-upload')).toBeVisible();
    await expect(page.locator('.user-name')).toHaveText('admin');
  });

  test('navigates to collections view when clicking collections link', async ({ page }) => {
    await page.goto('/');

    await page.locator('a.nav-link', { hasText: 'Collections' }).click();
    await expect(page).toHaveURL(/\/collections/);
    await expect(page.locator('text=Tournament Plays')).toBeVisible();
  });
});
