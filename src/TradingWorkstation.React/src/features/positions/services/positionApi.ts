import { httpClient } from "../../../shared/http/httpClient";
import type { Position } from "../models/Position";

export const getOpenPositions = async (
  clientId: string,
): Promise<Position[]> => {
  const response = await httpClient.get<Position[]>(`api/positions`, {
    params: {
      clientId,
    },
  });

  return response.data;
};
