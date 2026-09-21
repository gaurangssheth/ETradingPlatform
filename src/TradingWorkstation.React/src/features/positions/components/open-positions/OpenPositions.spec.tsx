import { render, screen } from "@testing-library/react";
import OpenPositions from "./OpenPositions";
import { http, HttpResponse } from "msw";
import { getMswTestController } from "../../../../testing/mswTestController";
import { positionMockData } from "../../mocks/positionMockData";

describe("OpenPositions", () => {
  it("loads and displays open positions", async () => {
    render(<OpenPositions />);

    expect(await screen.findByText("EURUSD")).toBeInTheDocument();

    expect(screen.getByText("100,000")).toBeInTheDocument();
  });

  it("shows an error when positions cannot be loaded", async () => {
    const msw = getMswTestController();

    msw.use(
      http.get("*/api/positions", () => {
        return HttpResponse.json({ message: "Server Error" }, { status: 500 });
      }),
    );

    render(<OpenPositions />);

    expect(
      await screen.findByText("Unable to load open positions."),
    ).toBeInTheDocument();
  });

  it("shows loading skeleton before positions are loaded", async () => {
    const msw = getMswTestController();

    msw.use(
      http.get("*/api/positions", async () => {
        return HttpResponse.json(positionMockData);
      }),
    );

    render(<OpenPositions />);

    expect(
      document.querySelectorAll(".open-positions-skeleton__cell"),
    ).not.toHaveLength(0);

    expect(await screen.findByText("EURUSD")).toBeInTheDocument();

    expect(
      document.querySelectorAll(".open-positions-skeleton__cell"),
    ).toHaveLength(0);
  });
});
