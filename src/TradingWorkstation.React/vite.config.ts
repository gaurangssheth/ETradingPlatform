import react from "@vitejs/plugin-react";
import { defineConfig } from "vitest/config";
import { playwright } from "@vitest/browser-playwright";

export default defineConfig({
  plugins: [react()],
  test: {
    globals: true,
    projects: [
      {
        test: {
          name: "unit",
          environment: "jsdom",
          setupFiles: [
            "./src/testing/vitest.setup.ts",
            "./src/testing/msw.server.setup.ts",
          ],
        },
      },
      {
        test: {
          name: "browser",
          setupFiles: [
            "./src/testing/vitest.setup.ts",
            "./src/testing/msw.browser.setup.ts",
          ],
          browser: {
            enabled: true,
            provider: playwright(),
            instances: [
              {
                browser: "chromium",
              },
            ],
          },
        },
      },
    ],
  },
});
