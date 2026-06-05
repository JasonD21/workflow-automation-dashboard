import { Component, inject } from '@angular/core';
import { RouterOutlet, RouterLink, RouterLinkActive, Router } from '@angular/router';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatListModule } from '@angular/material/list';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatMenuModule } from '@angular/material/menu';
import { AuthService } from '../core/auth.service';

@Component({
  standalone: true,
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    MatToolbarModule,
    MatSidenavModule,
    MatListModule,
    MatIconModule,
    MatButtonModule,
    MatMenuModule,
  ],
  template: ` <mat-toolbar color="primary">
      <span class="brand">Flowdesk</span>
      <span class="spacer"></span>
      <button mat-icon-button [matMenuTriggerFor]="menu">
        <mat-icon>account_circle</mat-icon>
      </button>
      <mat-menu #menu="matMenu">
        <div class="who">{{ auth.user()?.email }}</div>
        <button mat-menu-item (click)="logout()"><mat-icon>logout</mat-icon> Sign out</button>
      </mat-menu>
    </mat-toolbar>

    <mat-sidenav-container>
      <mat-sidenav mode="side" opened class="nav">
        <mat-nav-list>
          @for (item of nav; track item.path) {
            <a mat-list-item [routerLink]="item.path" routerLinkActive="active">
              <mat-icon matListItemIcon>{{ item.icon }}</mat-icon>
              <span matListItemTitle>{{ item.label }}</span>
            </a>
          }
        </mat-nav-list>
      </mat-sidenav>
      <mat-sidenav-content class="content">
        @if (auth.isDemo()) {
          <div class="demo-banner">
            Demo mode — you're exploring a sample account. Changes are read-only, but Test run and
            Generate now work live.
          </div>
        }
        <router-outlet />
      </mat-sidenav-content>
    </mat-sidenav-container>`,
  styles: [
    `
      :host {
        display: flex;
        flex-direction: column;
        height: 100vh;
      }
      .spacer {
        flex: 1;
      }
      .brand {
        font-weight: 600;
      }
      .who {
        padding: 8px 16px;
        color: #666;
        font-size: 13px;
      }
      mat-sidenav-container {
        flex: 1;
      }
      .nav {
        width: 220px;
        border-right: 1px solid #eee;
      }
      .demo-banner {
        background: #fff3e0;
        border: 1px solid #ffcc80;
        color: #6d4c00;
        padding: 10px 14px;
        border-radius: 8px;
        margin-bottom: 16px;
        font-size: 14px;
      }
      .active {
        background: rgba(63, 81, 181, 0.08);
      }
      .content {
        padding: 24px;
        background: #fafafa;
      }
    `,
  ],
})
export class ShellComponent {
  auth = inject(AuthService);
  private router = inject(Router);
  nav = [
    { path: '/dashboard', icon: 'dashboard', label: 'Dashboard' },
    { path: '/connections', icon: 'cable', label: 'Connections' },
    { path: '/automations', icon: 'bolt', label: 'Automations' },
    { path: '/activity', icon: 'history', label: 'Activity' },
    { path: '/reports', icon: 'mail', label: 'Reports' },
  ];
  logout() {
    this.auth.logout().subscribe(() => this.router.navigate(['/login']));
  }
}
