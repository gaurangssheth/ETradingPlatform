import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import TopNavigation from "./TopNavigation";

describe("TopNavigation", () => {
  it("marks the current route as active", () => {
    render(
      <MemoryRouter initialEntries={["/positions"]}>
        <TopNavigation />
      </MemoryRouter>,
    );

    const positionsLink = screen.getByRole("link", { name: /Positions/i });
    const dashboardLink = screen.getByRole("link", { name: /Dashboard/i });

    expect(positionsLink).toHaveClass("active");
    expect(dashboardLink).not.toHaveClass("active");
  });
});
