import { Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { DashboardService } from './dashboard.service';

@Component({
  standalone: true,
  imports: [RouterLink, MatCardModule, MatIconModule, MatButtonModule],
  template: ` @if (summary(); as s) {
      @if (needsReconnect().length) {
        <div class="banner">
          <mat-icon>warning</mat-icon>
          {{ needsReconnect().length }} connection(s) need reconnecting.
          <a routerLink="/connections">Fix</a>
        </div>
      }

      <div class="stats">
        <mat-card
          ><div class="big">{{ s.enabledAutomations }}</div>
          <div>active automations</div></mat-card
        >
        <mat-card
          ><div class="big">{{ s.runsLast7Days }}</div>
          <div>runs this week</div></mat-card
        >
        <mat-card
          ><div class="big" [class.bad]="s.failedRunsLast7Days">{{ s.failedRunsLast7Days }}</div>
          <div>failed</div></mat-card
        >
      </div>

      <div class="grid">
        <mat-card>
          <h3>Connections</h3>
          @for (c of s.connections; track c.id) {
            <div class="row">
              <span>{{ pretty(c.provider) }}</span>
              <span
                class="pill"
                [class.ok]="c.status === 'Active'"
                [class.warn]="c.status !== 'Active'"
                >{{ c.status }}</span
              >
            </div>
          } @empty {
            <p class="muted">No connections yet. <a routerLink="/connections">Connect one</a></p>
          }
        </mat-card>

        <mat-card>
          <h3>Recent activity</h3>
          @for (r of s.recentRuns; track r.id) {
            <div class="row">
              <span
                >{{ r.automationName }}
                @if (r.isTest) {
                  <em>(test)</em>
                }
              </span>
              <span
                class="pill"
                [class.ok]="r.status === 'Success'"
                [class.bad-pill]="r.status === 'Failed'"
                >{{ r.status }}</span
              >
            </div>
          } @empty {
            <p class="muted">No runs yet.</p>
          }
        </mat-card>

        <mat-card>
          <h3>Next report</h3>
          @if (s.nextReport; as n) {
            <p>
              <b>{{ n.name }}</b
              ><br />{{ n.dayOfWeek }} at {{ n.timeOfDay }}
            </p>
          } @else {
            <p class="muted">No scheduled reports. <a routerLink="/reports">Create one</a></p>
          }
        </mat-card>
      </div>
    } @else {
      <p>Loading…</p>
    }`,
  styles: [
    `
      .banner {
        display: flex;
        align-items: center;
        gap: 8px;
        background: #fff3e0;
        border: 1px solid #ffcc80;
        padding: 10px 14px;
        border-radius: 8px;
        margin-bottom: 16px;
      }
      .stats {
        display: grid;
        grid-template-columns: repeat(3, 1fr);
        gap: 16px;
        margin-bottom: 16px;
      }
      .stats mat-card {
        text-align: center;
        padding: 16px;
      }
      .big {
        font-size: 36px;
        font-weight: 700;
      }
      .big.bad {
        color: #c00;
      }
      .grid {
        display: grid;
        grid-template-columns: repeat(3, 1fr);
        gap: 16px;
      }
      .grid mat-card {
        padding: 16px;
      }
      h3 {
        margin-top: 0;
      }
      .row {
        display: flex;
        justify-content: space-between;
        padding: 6px 0;
        border-bottom: 1px solid #f3f3f3;
      }
      .pill {
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
      .pill.bad-pill {
        background: #ffebee;
        color: #c62828;
      }
      .muted {
        color: #888;
      }
    `,
  ],
})
export class DashboardComponent {
  private dashboard = inject(DashboardService);
  summary = toSignal(this.dashboard.getSummary(), { initialValue: null });
  needsReconnect = computed(
    () => this.summary()?.connections.filter((c) => c.status === 'NeedsReconnect') ?? [],
  );
  pretty(p: string) {
    return p === 'GoogleCalendar' ? 'Google Calendar' : p === 'QuickBooks' ? 'QuickBooks' : p;
  }
}
