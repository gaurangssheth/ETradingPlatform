import { render, screen } from '@testing-library/angular';
import { TopNavigationComponent } from './top-navigation.component';
import { provideRouter, Router } from '@angular/router';

describe('TopNavigationComponent', () => {
  let router: Router;
  beforeEach(async () => {
    const { fixture } = await render(TopNavigationComponent, {
      providers: [
        provideRouter([
          {
            path: '',
            component: TopNavigationComponent,
          },
          {
            path: 'orders',
            component: TopNavigationComponent,
          },
          {
            path: 'positions',
            component: TopNavigationComponent,
          },
        ]),
      ],
    });
    const injector = fixture.debugElement.injector;
    router = injector.get(Router);
  });

  it('should mark the current route as active', async () => {
    await router.navigateByUrl('/positions');

    const positionsLink = screen.getByRole('link', { name: /Positions/i });
    const dashboardLink = screen.getByRole('link', {
      name: 'Dashboard',
    });

    expect(positionsLink).toHaveClass('active');
    expect(dashboardLink).not.toHaveClass('active');
  });

  it('can navigate to orders', async () => {
    await router.navigateByUrl('/orders');

    expect(router.url).toBe('/orders');
  });
});
