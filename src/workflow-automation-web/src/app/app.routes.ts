import { Routes } from '@angular/router';
import { authGuard } from './core/auth.guard';

export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () => import('./auth/login.component').then((m) => m.LoginComponent),
  },
  {
    path: 'register',
    loadComponent: () => import('./auth/register.component').then((m) => m.RegisterComponent),
  },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./shell/shell.component').then((m) => m.ShellComponent),
    children: [
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
      {
        path: 'dashboard',
        loadComponent: () =>
          import('./dashboard/dashboard.component').then((m) => m.DashboardComponent),
      },
      {
        path: 'connections',
        loadComponent: () =>
          import('./connections/connections.component').then((m) => m.ConnectionsComponent),
      },
      {
        path: 'automations',
        loadComponent: () =>
          import('./automations/automations-list.component').then(
            (m) => m.AutomationsListComponent,
          ),
      },
      {
        path: 'automations/new',
        loadComponent: () =>
          import('./automations/builder.component').then((m) => m.BuilderComponent),
      },
      {
        path: 'automations/:id/edit',
        loadComponent: () =>
          import('./automations/builder.component').then((m) => m.BuilderComponent),
      },
      {
        path: 'activity',
        loadComponent: () => import('./runs/activity.component').then((m) => m.ActivityComponent),
      },
      {
        path: 'reports',
        loadComponent: () => import('./reports/reports.component').then((m) => m.ReportsComponent),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
