import type { RequestHandler } from "msw";

type MswTestController = {
  use: (...handlers: RequestHandler[]) => void;
  resetHandlers: (...handlers: RequestHandler[]) => void;
};

let controller: MswTestController | undefined;

export const setMswTestController = (
  mswController: MswTestController,
): void => {
  controller = mswController;
};

export const getMswTestController = (): MswTestController => {
  if (!controller) {
    throw new Error("MSW test controller has not been configured.");
  }

  return controller;
};
