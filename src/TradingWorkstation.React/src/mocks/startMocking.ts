const apiMode = import.meta.env.VITE_API_MODE;

export const startMocking = async (): Promise<void> => {
  if (apiMode !== "mock") {
    return;
  }

  const browserModule = await import("./browser");
  const browserWorker = browserModule.browserWorker;

  await browserWorker.start({
    onUnhandledRequest: "bypass",
  });
};
