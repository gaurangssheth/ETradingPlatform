import type { RouteObject } from "react-router-dom";
import TradingDashboardPage from "../pages/trading-dashboard/TradingDashboardPage";

export const routes: RouteObject[] = [
  {
    path: "/",
    element: <TradingDashboardPage />,
  },
];
