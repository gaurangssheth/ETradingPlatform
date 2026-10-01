import "./MarketWatchSkeleton.scss";

export const MarketWatchSkeleton = () => (
  <>
    {Array.from({ length: 3 }).map((_, index) => (
      <tr key={index}>
        <td className="symbol-column">
          <span className="market-watch-skeleton__cell" />
        </td>

        <td className="numeric-column">
          <span className="market-watch-skeleton__cell" />
        </td>

        <td className="numeric-column">
          <span className="market-watch-skeleton__cell" />
        </td>

        <td className="numeric-column">
          <span className="market-watch-skeleton__cell" />
        </td>
      </tr>
    ))}
  </>
);
