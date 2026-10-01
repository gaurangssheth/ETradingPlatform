import { useRoutes } from "react-router-dom";
import { routes } from "./routes";
import "./App.scss";
import { ToastContainer } from "react-toastify";

const App = () => {
  return (
    <>
      {useRoutes(routes)}
      <ToastContainer
        position="top-right"
        autoClose={5000}
        closeOnClick
        pauseOnHover
        draggable
        theme="colored"
      />
    </>
  );
};

export default App;
