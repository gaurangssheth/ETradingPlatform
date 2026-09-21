import { NavLink } from "react-router-dom";
import "./TopNavigation.scss";

const TopNavigation = () => {
  return (
    <header className="top-navigation">
      <div className="top-navigation__brand">ETrading Platform</div>

      <nav className="top-navigation__links">
        <NavLink to="/">Dashboard</NavLink>
        <NavLink to="/orders">Orders</NavLink>
        <NavLink to="/trades">Trades</NavLink>
        <NavLink to="/positions">Positions</NavLink>
        <NavLink to="/risk">Risk</NavLink>
      </nav>
    </header>
  );
};

export default TopNavigation;
