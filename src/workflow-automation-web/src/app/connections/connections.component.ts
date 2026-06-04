import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ConnectionsService } from './connections.service';
import { Connection } from '../core/models';

@Component({
  standalone: true,
  imports: [MatCardModule, MatButtonModule, MatIconModule],
  template: ` <h1>Connections</h1>
    <p class="muted">Link the tools your automations and reports read from.</p>

    <div class="grid">
      @for (p of providers; track p.key) {
        @let conn = byProvider()[p.key];
        <mat-card>
          <div class="head">
            <mat-icon>{{ p.icon }}</mat-icon>
            <span class="name">{{ p.label }}</span>
            @if (conn) {
              <span
                class="pill"
                [class.ok]="conn.status === 'Active'"
                [class.warn]="conn.status !== 'Active'"
              >
                {{ conn.status }}
              </span>
            }
          </div>

          @if (conn) {
            <p class="sub">{{ conn.displayName }}</p>
            <div class="actions">
              @if (conn.status !== 'Active') {
                <button mat-flat-button color="primary" (click)="connect(p.key)">Reconnect</button>
              }
              <button mat-stroked-button color="warn" (click)="disconnect(conn)">Disconnect</button>
            </div>
          } @else {
            <p class="sub muted">Not connected</p>
            <button mat-flat-button color="primary" (click)="connect(p.key)">
              <mat-icon>add_link</mat-icon> Connect
            </button>
          }
        </mat-card>
      }
    </div>`,
  styles: [
    `
      .grid {
        display: grid;
        grid-template-columns: repeat(auto-fill, minmax(240px, 1fr));
        gap: 16px;
        max-width: 840px;
      }
      mat-card {
        padding: 16px;
      }
      .head {
        display: flex;
        align-items: center;
        gap: 8px;
      }
      .name {
        font-weight: 600;
      }
      .pill {
        margin-left: auto;
        font-size: 12px;
        padding: 2px 8px;
        border-radius: 10px;
        background: #eee;
      }
      .pill.ok {
        background: #e8f5e9;
        color: #2e7d32;
      }
      .pill.warn {
        background: #fff3e0;
        color: #e65100;
      }
      .sub {
        margin: 8px 0;
      }
      .muted {
        color: #888;
      }
      .actions {
        display: flex;
        gap: 8px;
      }
    `,
  ],
})
export class ConnectionsComponent implements OnInit {
  private svc = inject(ConnectionsService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private snack = inject(MatSnackBar);

  connections = signal<Connection[]>([]);
  providers = [
    { key: 'Slack', label: 'Slack', icon: 'forum' },
    { key: 'QuickBooks', label: 'QuickBooks', icon: 'receipt_long' },
    { key: 'GoogleCalendar', label: 'Google Calendar', icon: 'event' },
  ];
  byProvider = computed(
    () =>
      Object.fromEntries(this.connections().map((c) => [c.provider, c])) as Record<
        string,
        Connection
      >,
  );

  ngOnInit() {
    const q = this.route.snapshot.queryParamMap;
    const status = q.get('status');
    if (status === 'connected')
      this.snack.open(`${q.get('provider')} connected`, 'OK', { duration: 3000 });
    else if (status === 'error')
      this.snack.open('Connection failed — please try again', 'OK', { duration: 4000 });
    if (status) this.router.navigate([], { queryParams: {}, replaceUrl: true });
    this.load();
  }

  load() {
    this.svc.list().subscribe((c) => this.connections.set(c));
  }

  connect(provider: string) {
    this.svc.authorize(provider).subscribe({
      next: (r: any) => {
        const url = typeof r === 'string' ? r : (r?.url ?? r?.authorizeUrl);
        if (!url) {
          console.error('No authorize URL in response:', r);
          return;
        }
        window.location.href = url;
      },
      error: (e) => console.error('authorize failed:', e),
    });
  }

  disconnect(c: Connection) {
    if (!confirm(`Disconnect ${c.provider}? Automations that use it will be disabled.`)) return;
    this.svc.disconnect(c.id).subscribe(() => {
      this.snack.open(`${c.provider} disconnected`, 'OK', { duration: 3000 });
      this.load();
    });
  }
}
