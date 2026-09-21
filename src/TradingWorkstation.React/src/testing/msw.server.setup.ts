import { server } from "../mocks/server";
import { setMswTestController } from "./mswTestController";

beforeAll(() => {
  server.listen({
    onUnhandledRequest: "error",
  });

  setMswTestController(server);
});

afterEach(() => {
  server.resetHandlers();
});

afterAll(() => {
  server.close();
});
