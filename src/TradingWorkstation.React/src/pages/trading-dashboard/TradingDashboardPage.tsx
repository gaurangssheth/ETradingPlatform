import MarketWatch from "../../features/market/components/MarketWatch";
import { selectedSymbolSelector } from "../../features/market/state/marketSlice";
import OpenPositions from "../../features/positions/components/open-positions/OpenPositions";
import TopNavigation from "../../shared/components/top-navigation/TopNavigation";
import { useAppSelector } from "../../store/hooks";

import "./TradingDashboardPage.scss";

const TradingDashboardPage = () => {
  const selectedSymbol = useAppSelector(selectedSymbolSelector);
  return (
    <>
      <TopNavigation />

      <main className="trading-dashboard">
        <section className="trading-dashboard__market-watch dashboard-panel">
          <MarketWatch />
        </section>

        <section className="trading-dashboard__instrument dashboard-panel">
          <div className="dashboard-panel__header">{selectedSymbol}</div>

          <div className="trading-dashboard__price">
            <span>1.0849</span>
            <span className="trading-dashboard__price-separator">/</span>
            <span>1.0851</span>
          </div>

          <div className="trading-dashboard__chart-placeholder">
            Price chart
          </div>
        </section>

        <div className="trading-dashboard__positions">
          <OpenPositions />
        </div>

        <section className="trading-dashboard__order-ticket dashboard-panel">
          <div className="dashboard-panel__header">Order Ticket</div>

          <div className="dashboard-panel__content">
            <div className="trading-dashboard__side-buttons">
              <button type="button">Buy</button>
              <button type="button">Sell</button>
            </div>

            <div className="trading-dashboard__ticket-placeholder">
              Quantity / Order Type / Price
            </div>
          </div>
        </section>

        <section className="trading-dashboard__activity dashboard-panel">
          <div className="dashboard-panel__header">Recent Activity</div>

          <div className="dashboard-panel__content">
            Recent orders and trades will appear here.
          </div>
        </section>
      </main>
    </>
  );
};

export default TradingDashboardPage;
