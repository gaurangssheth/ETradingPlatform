import { createSlice, type PayloadAction } from "@reduxjs/toolkit";
import type { RootState } from "../../../store/store";

type MarketState = {
  selectedSymbol: string;
};

const initialState: MarketState = {
  selectedSymbol: "EURUSD",
};

const marketSlice = createSlice({
  name: "market",
  initialState,
  reducers: {
    selectSymbol: (state, action: PayloadAction<string>) => {
      state.selectedSymbol = action.payload;
    },
  },
});

export const { selectSymbol } = marketSlice.actions;

export const marketReducer = marketSlice.reducer;

export const selectedSymbolSelector = (state: RootState): string =>
  state.market.selectedSymbol;
