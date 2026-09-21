import { browserWorker } from "../mocks/browser";
import { setMswTestController } from "./mswTestController";

beforeAll(async () => {
  await browserWorker.start({
    onUnhandledRequest: "error",
  });

  setMswTestController(browserWorker);
});

afterAll(() => {
  browserWorker.stop();
});
