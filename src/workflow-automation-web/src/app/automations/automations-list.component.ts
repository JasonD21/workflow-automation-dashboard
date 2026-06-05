import { Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSnackBar } from '@angular/material/snack-bar';
import { AutomationsService } from './automations.service';
import { CatalogService } from './catalog.service';
import { Automation } from '../core/models';
import { AuthService } from '../core/auth.service';

@Component({
  standalone: true,
  imports: [
    DatePipe,
    RouterLink,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatSlideToggleModule,
  ],
  template: ` <div class="head">
      <h1>Automations</h1>
      @if (!auth.isDemo()) {
        <a mat-flat-button color="primary" routerLink="/automations/new"
          ><mat-icon>add</mat-icon> New automation</a
        >
      }
    </div>

    @if (loading()) {
      <p>Loading…</p>
    } @else {
      @for (a of automations(); track a.id) {
        <mat-card [class.disabled]="!a.isEnabled">
          <div class="row">
            <div class="info">
              <div class="title">{{ a.name }}</div>
              <div class="flow">
                {{ label(a.triggerType) }} <mat-icon>arrow_forward</mat-icon>
                {{ label(a.actionType) }}
              </div>
              <div class="meta">
                @if (a.lastTriggeredAt) {
                  Last run {{ a.lastTriggeredAt | date: 'medium' }}
                } @else {
                  Never run
                }
              </div>
            </div>
            <div class="ctrls">
              <mat-slide-toggle
                [checked]="a.isEnabled"
                (change)="toggle(a, $event.checked)"
                [disabled]="auth.isDemo()"
              />
              <button mat-icon-button title="Test run" (click)="test(a)">
                <mat-icon>play_arrow</mat-icon>
              </button>
              <!-- always shown -->
              @if (!auth.isDemo()) {
                <a mat-icon-button [routerLink]="['/automations', a.id, 'edit']"
                  ><mat-icon>edit</mat-icon></a
                >
                <button mat-icon-button (click)="remove(a)"><mat-icon>delete</mat-icon></button>
              }
            </div>
          </div>
        </mat-card>
      } @empty {
        <mat-card class="empty">
          <mat-icon>bolt</mat-icon>
          <p>No automations yet. Connect your tools, then build your first one.</p>
          <a mat-flat-button color="primary" routerLink="/automations/new">Create automation</a>
        </mat-card>
      }
    }`,
  styles: [
    `
      .head {
        display: flex;
        justify-content: space-between;
        align-items: center;
        margin-bottom: 16px;
      }
      mat-card {
        padding: 14px 18px;
        margin-bottom: 12px;
      }
      mat-card.disabled {
        opacity: 0.55;
      }
      .row {
        display: flex;
        justify-content: space-between;
        align-items: center;
        gap: 12px;
      }
      .title {
        font-weight: 600;
      }
      .flow {
        display: flex;
        align-items: center;
        gap: 6px;
        color: #555;
        margin: 4px 0;
      }
      .flow mat-icon {
        font-size: 16px;
        width: 16px;
        height: 16px;
      }
      .meta {
        font-size: 12px;
        color: #999;
      }
      .ctrls {
        display: flex;
        align-items: center;
        gap: 2px;
      }
      .empty {
        text-align: center;
        padding: 40px;
      }
      .empty mat-icon {
        font-size: 40px;
        width: 40px;
        height: 40px;
        color: #bbb;
      }
    `,
  ],
})
export class AutomationsListComponent implements OnInit {
  private svc = inject(AutomationsService);
  private catalog = inject(CatalogService);
  private snack = inject(MatSnackBar);
  auth = inject(AuthService);

  automations = signal<Automation[]>([]);
  loading = signal(true);
  private labels = new Map<string, string>();

  ngOnInit() {
    forkJoin({
      autos: this.svc.list(),
      triggers: this.catalog.triggers(),
      actions: this.catalog.actions(),
    }).subscribe(({ autos, triggers, actions }) => {
      [...triggers, ...actions].forEach((d) => this.labels.set(d.type, d.displayName));
      this.automations.set(autos);
      this.loading.set(false);
    });
  }

  label(type: string) {
    return this.labels.get(type) ?? type;
  }

  toggle(a: Automation, enabled: boolean) {
    this.svc.setEnabled(a.id, enabled).subscribe(() => {
      this.automations.update((list) =>
        list.map((x) => (x.id === a.id ? { ...x, isEnabled: enabled } : x)),
      );
    });
  }

  test(a: Automation) {
    this.snack.open('Running test…', undefined, { duration: 1500 });
    this.svc.test(a.id).subscribe((run) => {
      const msg =
        run.status === 'Success'
          ? 'Test succeeded — check the action target'
          : run.status === 'Skipped'
            ? 'Test skipped (filter not matched)'
            : `Test failed: ${run.errorMessage ?? 'unknown error'}`;
      this.snack.open(msg, 'OK', { duration: 5000 });
    });
  }

  remove(a: Automation) {
    if (!confirm(`Delete "${a.name}"? Its run history is kept.`)) return;
    this.svc.delete(a.id).subscribe(() => {
      this.automations.update((list) => list.filter((x) => x.id !== a.id));
      this.snack.open('Automation deleted', 'OK', { duration: 3000 });
    });
  }
}
