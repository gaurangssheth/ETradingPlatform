import type { RequestHandler } from "msw";

declare module "@/mocks/runtime" {
  export const mockServer: {
    use(...handlers: RequestHandler[]): void;
  };
}
