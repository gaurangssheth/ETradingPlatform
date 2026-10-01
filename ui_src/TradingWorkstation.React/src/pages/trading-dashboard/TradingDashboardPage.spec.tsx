// TradingDashboardPage.spec.tsx
import { screen } from "@testing-library/react";
import TradingDashboardPage from "./TradingDashboardPage";
import { MemoryRouter } from "react-router-dom";
import { renderWithProviders } from "../../testing/renderWithProviders";

describe("TradingDashboardPage", () => {
  it("renders the Trading Workstation heading", () => {
    renderWithProviders(
      <MemoryRouter>
        <TradingDashboardPage />
      </MemoryRouter>,
    );

    expect(screen.getByText("ETrading Platform")).toBeInTheDocument();
  });
});
