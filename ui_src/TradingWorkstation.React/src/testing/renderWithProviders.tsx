import { render, type RenderResult } from "@testing-library/react";
import type { ReactElement } from "react";
import { store } from "../store/store";
import { Provider } from "react-redux";

export const renderWithProviders = (component: ReactElement): RenderResult => {
  return render(<Provider store={store}>{component}</Provider>);
};
