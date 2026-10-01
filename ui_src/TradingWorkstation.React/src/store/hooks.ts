import { useDispatch, useSelector } from "react-redux";
import type { AppDispatch, RootState } from "./store";

export const userAppDisptch = useDispatch.withTypes<AppDispatch>();

export const useAppSelector = useSelector.withTypes<RootState>();
