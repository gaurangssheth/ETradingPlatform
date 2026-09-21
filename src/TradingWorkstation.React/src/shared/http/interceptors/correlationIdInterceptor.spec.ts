import axios, { type AxiosAdapter, type AxiosResponse } from "axios";
import { addCorrelationIdInterceptor } from "./correlationIdInterceptor";

describe("addCorrelationIdInterceptor", () => {
  it("adds correlationid to the request", async () => {
    const correlationId = "11111111-1111-4111-8111-111111111111";

    vi.spyOn(crypto, "randomUUID").mockReturnValue(correlationId);

    let capturedCorrelationId: string | undefined;

    const adapter: AxiosAdapter = async (config) => {
      capturedCorrelationId = config.headers["X-Correlation-Id"]?.toString();
      const response: AxiosResponse = {
        data: {},
        status: 200,
        statusText: "OK",
        headers: {},
        config,
      };

      return response;
    };

    const httpClient = axios.create({
      adapter,
    });

    addCorrelationIdInterceptor(httpClient);

    await httpClient.get("/test");

    expect(capturedCorrelationId).toBeDefined();
    expect(capturedCorrelationId).toBe(correlationId);
  });
});
