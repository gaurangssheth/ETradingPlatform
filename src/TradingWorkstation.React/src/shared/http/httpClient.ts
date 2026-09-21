import axios from "axios";
import { addCorrelationIdInterceptor } from "./interceptors/correlationIdInterceptor";
import { addHttpErrorInterceptor } from "./interceptors/httpErrorInterceptor";

console.log("API URL:", import.meta.env.VITE_API_BASE_URL);

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL;

if (!apiBaseUrl) {
  throw new Error("VITE_API_BASE_URL is not configured.");
}

export const httpClient = axios.create({
  baseURL: apiBaseUrl,
  timeout: 10000,
});

addCorrelationIdInterceptor(httpClient);
addHttpErrorInterceptor(httpClient);
