import { useEffect, useState } from "react";
import type { Position } from "../../models/Position";
import "./OpenPositions.scss";
import "./OpenPositionsSkeleton.scss";
import { getOpenPositions } from "../../services/positionApi";
import { notifications } from "../../../../shared/notifications/notificationService";

const OpenPositions = () => {
  const [positions, setPositions] = useState<Position[]>([]);
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const loadingPositions = async () => {
      try {
        const result = await getOpenPositions("client-001");
        setPositions(result);
      } catch {
        setError("Unable to load open positions.");
        notifications.error("Unable to load open positions.");
      } finally {
        setIsLoading(false);
      }
    };

    loadingPositions();
  }, []);

  const renderHeader = () => (
    <thead>
      <tr>
        <th className="symbol-column">Symbol</th>
        <th className="numeric-column">Net Quantity</th>
        <th className="numeric-column">Average Price</th>
        <th className="numeric-column">Realised P&amp;L</th>
        <th className="numeric-column">Unrealised P&amp;L</th>
        <th className="numeric-column">Total P&amp;L</th>
      </tr>
    </thead>
  );

  const renderLoadingRows = () => (
    <>
      {Array.from({ length: 4 }).map((_, index) => (
        <tr key={index}>
          <td className="symbol-column">
            <span className="open-positions-skeleton__cell" />
          </td>

          <td className="numeric-column">
            <span className="open-positions-skeleton__cell" />
          </td>

          <td className="numeric-column">
            <span className="open-positions-skeleton__cell" />
          </td>

          <td className="numeric-column">
            <span className="open-positions-skeleton__cell" />
          </td>

          <td className="numeric-column">
            <span className="open-positions-skeleton__cell" />
          </td>

          <td className="numeric-column">
            <span className="open-positions-skeleton__cell" />
          </td>
        </tr>
      ))}
    </>
  );

  const renderOpenPositions = () => (
    <>
      {positions.map((position) => {
        const totalPnl = position.realisedPnl + position.unrealisedPnl;

        return (
          <tr key={position.instrumentId}>
            <td className="symbol-column">{position.symbol}</td>

            <td className="numeric-column">
              {position.netQuantity.toLocaleString()}
            </td>

            <td className="numeric-column">
              {position.averagePrice.toFixed(4)} {position.pnlCurrency}
            </td>

            <td className="numeric-column">
              {position.realisedPnl.toFixed(2)} {position.pnlCurrency}
            </td>

            <td className="numeric-column">
              {position.unrealisedPnl.toFixed(2)} {position.pnlCurrency}
            </td>

            <td className="numeric-column">
              {totalPnl.toFixed(2)} {position.pnlCurrency}
            </td>
          </tr>
        );
      })}
    </>
  );

  const renderError = () => (
    <tr>
      <td colSpan={6} className="open-positions__error">
        Unable to load open positions.
      </td>
    </tr>
  );

  const renderBody = () => {
    if (isLoading) {
      return renderLoadingRows();
    }

    if (error) {
      return renderError();
    }

    return renderOpenPositions();
  };

  return (
    <section className="open-positions">
      <div className="open-positions__header">
        <h2>Open Positions</h2>
      </div>

      <div className="open-positions__table-wrapper">
        <table className="data-table">
          {renderHeader()}
          <tbody>{renderBody()}</tbody>
        </table>
      </div>
    </section>
  );
};

export default OpenPositions;
