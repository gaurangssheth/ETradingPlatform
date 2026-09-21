import axios, { Axios, AxiosError, type AxiosAdapter } from "axios";
import { addHttpErrorInterceptor } from "./httpErrorInterceptor";
import { notifications } from "../../notifications/notificationService";

describe("addHttpErrorInterceptor", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("shows a server error notification for a 500 response", async () => {
    vi.spyOn(notifications, "error").mockImplementation(() => {});

    const adapter: AxiosAdapter = async (config) => {
      const error = new axios.AxiosError(
        "Server error",
        undefined,
        config,
        undefined,
        {
          data: {},
          status: 500,
          statusText: "Internal Server Error",
          headers: {},
          config,
        },
      );

      throw error;
    };

    const httpClient = axios.create({
      adapter,
    });

    addHttpErrorInterceptor(httpClient);

    await expect(httpClient.get("/test")).rejects.toBeInstanceOf(AxiosError);

    expect(notifications.error).toHaveBeenCalledWith(
      "A server error occurred.",
    );
  });

  it("shows a connection error notification when there is no response", async () => {
    vi.spyOn(notifications, "error").mockReturnValue(undefined);

    const adapter: AxiosAdapter = async (config) => {
      throw new axios.AxiosError("Network Error", "ERR_NETWORK", config);
    };

    const httpClient = axios.create({
      adapter,
    });

    addHttpErrorInterceptor(httpClient);

    await expect(httpClient.get("/test")).rejects.toBeInstanceOf(
      axios.AxiosError,
    );

    expect(notifications.error).toHaveBeenCalledWith(
      "Unable to connect to the server.",
    );
  });

  it("shows a connection error notification when there is time out", async () => {
    vi.spyOn(notifications, "error").mockReturnValue(undefined);

    const adapter: AxiosAdapter = async (config) => {
      throw new axios.AxiosError("Timeout", "ECONNABORTED", config);
    };

    const httpClient = axios.create({
      adapter,
    });

    addHttpErrorInterceptor(httpClient);

    await expect(httpClient.get("/test")).rejects.toBeInstanceOf(
      axios.AxiosError,
    );

    expect(notifications.error).toHaveBeenCalledWith("The request timed out.");
  });
});
