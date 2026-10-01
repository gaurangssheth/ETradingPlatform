import type { AxiosInstance } from "axios";
import axios from "axios";
import { notifications } from "../../notifications/notificationService";

export const addHttpErrorInterceptor = (httpClient: AxiosInstance): void => {
  httpClient.interceptors.response.use(
    (response) => response,
    (error: unknown) => {
      if (axios.isAxiosError(error)) {
        if (error.code === "ECONNABORTED") {
          notifications.error("The request timed out.");
        } else if (!error.response) {
          notifications.error("Unable to connect to the server.");
        } else if (error.response.status >= 500) {
          notifications.error("A server error occurred.");
        }
      }

      return Promise.reject(error);
    },
  );
};
