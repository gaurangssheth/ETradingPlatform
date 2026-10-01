import { screen, render } from '@testing-library/angular';
import { TradingDashboardComponent } from './trading-dashboard.component';

describe('TradingDashboardComponent', () => {
  it('should render the Trading Workstation heading', async () => {
    await render(TradingDashboardComponent);

    const heading = screen.getByRole('heading', {
      name: /Trading Workstation/i,
    });

    expect(heading).toBeInTheDocument();
  });
});
