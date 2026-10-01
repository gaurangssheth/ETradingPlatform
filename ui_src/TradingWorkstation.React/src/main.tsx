import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import "./index.css";
import { BrowserRouter } from "react-router-dom";
import App from "./app/App";
import "./styles.scss";
import { startMocking } from "./mocks/startMocking";
import { Provider } from "react-redux";
import { store } from "./store/store";
import { ErrorBoundary } from "./shared/components/ErrorBoundary/ErrorBoundary";

const startApplication = async (): Promise<void> => {
  await startMocking();

  createRoot(document.getElementById("root")!).render(
    <StrictMode>
      <Provider store={store}>
        <BrowserRouter>
          <ErrorBoundary>
            <App />
          </ErrorBoundary>
        </BrowserRouter>
      </Provider>
    </StrictMode>,
  );
};

startApplication();
