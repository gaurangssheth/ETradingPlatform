// TradingDashboardPage.spec.tsx
import { render, screen } from "@testing-library/react";
import TradingDashboardPage from "./TradingDashboardPage";
import { MemoryRouter } from "react-router-dom";

describe("TradingDashboardPage", () => {
  it("renders the Trading Workstation heading", () => {
    render(
      <MemoryRouter>
        <TradingDashboardPage />
      </MemoryRouter>,
    );

    expect(screen.getByText("ETrading Platform")).toBeInTheDocument();
  });
});
