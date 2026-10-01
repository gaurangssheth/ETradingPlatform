import type { AxiosInstance } from "axios";

export const addCorrelationIdInterceptor = (
  httpClient: AxiosInstance,
): void => {
  httpClient.interceptors.request.use((config) => {
    config.headers["X-Correlation-Id"] = crypto.randomUUID();

    return config;
  });
};
