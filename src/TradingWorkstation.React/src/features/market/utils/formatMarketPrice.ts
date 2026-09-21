export const formatMarketPrice = (symbol: string, value: number): string => {
  if (symbol === "EURUSD") {
    return value.toFixed(4);
  }

  return value.toFixed(2);
};
