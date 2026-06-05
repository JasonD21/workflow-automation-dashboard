import { Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ReportsService } from './reports.service';
import { ScheduleDialogComponent } from './schedule-dialog.component';
import { ReportViewerComponent } from './report-viewer.component';
import { ReportSchedule, GeneratedReportSummary, SaveReportSchedule } from '../core/models';
import { AuthService } from '../core/auth.service';

@Component({
  standalone: true,
  imports: [DatePipe, MatCardModule, MatButtonModule, MatIconModule, MatSlideToggleModule],
  template: ` <div class="head">
      <h1>Reports</h1>
      @if (!auth.isDemo()) {
        <button mat-flat-button color="primary" (click)="openCreate()">
          <mat-icon>add</mat-icon> New schedule
        </button>
      }
    </div>

    @for (s of schedules(); track s.id) {
      <mat-card [class.disabled]="!s.isEnabled">
        <div class="row">
          <div>
            <div class="title">{{ s.name }}</div>
            <div class="meta">
              {{ s.dayOfWeek }} at {{ s.timeOfDay.substring(0, 5) }} · {{ s.timeZone }}
            </div>
            <div class="meta">
              {{ s.includedSources.length }} sources · to {{ s.recipientEmail }}
              @if (s.lastRunAt) {
                · last sent {{ s.lastRunAt | date: 'short' }}
              }
            </div>
          </div>
          <div class="ctrls">
            @if (!auth.isDemo()) {
              <mat-slide-toggle [checked]="s.isEnabled" (change)="toggle(s, $event.checked)" />
            }
            <button mat-stroked-button (click)="generate(s)">
              <mat-icon>send</mat-icon> Generate now
            </button>
            @if (!auth.isDemo()) {
              <button mat-icon-button (click)="openEdit(s)"><mat-icon>edit</mat-icon></button>
              <button mat-icon-button (click)="remove(s)"><mat-icon>delete</mat-icon></button>
            }
          </div>
        </div>
      </mat-card>
    } @empty {
      <mat-card class="empty"
        ><mat-icon>mail</mat-icon>
        <p>
          No report schedules yet. Create one to get a weekly digest of your invoices, meetings, and
          activity.
        </p>
      </mat-card>
    }

    <h2>Recent reports</h2>
    @for (r of reports(); track r.id) {
      <div class="report" (click)="open(r)">
        <span>{{ r.generatedAt | date: 'medium' }}</span>
        <span class="period"
          >{{ r.periodStart | date: 'MMM d' }} – {{ r.periodEnd | date: 'MMM d' }}</span
        >
        <span
          class="pill"
          [class.ok]="r.emailStatus === 'Sent'"
          [class.bad]="r.emailStatus === 'Failed'"
          >{{ r.emailStatus }}</span
        >
      </div>
    } @empty {
      <p class="muted">No reports generated yet.</p>
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
      .meta {
        font-size: 12px;
        color: #888;
      }
      .ctrls {
        display: flex;
        align-items: center;
        gap: 6px;
      }
      .empty {
        text-align: center;
        padding: 40px;
      }
      h2 {
        margin-top: 28px;
      }
      .report {
        display: flex;
        gap: 16px;
        align-items: center;
        padding: 10px 14px;
        border-bottom: 1px solid #eee;
        cursor: pointer;
      }
      .report:hover {
        background: #f7f7f7;
      }
      .period {
        color: #888;
        flex: 1;
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
      .pill.bad {
        background: #ffebee;
        color: #c62828;
      }
      .muted {
        color: #999;
      }
    `,
  ],
})
export class ReportsComponent implements OnInit {
  private svc = inject(ReportsService);
  private dialog = inject(MatDialog);
  private snack = inject(MatSnackBar);
  auth = inject(AuthService);
  schedules = signal<ReportSchedule[]>([]);
  reports = signal<GeneratedReportSummary[]>([]);

  ngOnInit() {
    this.loadSchedules();
    this.loadReports();
  }
  loadSchedules() {
    this.svc.listSchedules().subscribe((s) => this.schedules.set(s));
  }
  loadReports() {
    this.svc.listReports().subscribe((r) => this.reports.set(r.items));
  }

  openCreate() {
    this.dialog
      .open(ScheduleDialogComponent, { data: null, width: '480px' })
      .afterClosed()
      .subscribe((saved) => saved && this.loadSchedules());
  }
  openEdit(s: ReportSchedule) {
    this.dialog
      .open(ScheduleDialogComponent, { data: s, width: '480px' })
      .afterClosed()
      .subscribe((saved) => saved && this.loadSchedules());
  }

  toggle(s: ReportSchedule, enabled: boolean) {
    const payload: SaveReportSchedule = {
      ...s,
      isEnabled: enabled,
      recipientEmail: s.recipientEmail,
    };
    this.svc
      .updateSchedule(s.id, payload)
      .subscribe(() =>
        this.schedules.update((list) =>
          list.map((x) => (x.id === s.id ? { ...x, isEnabled: enabled } : x)),
        ),
      );
  }

  generate(s: ReportSchedule) {
    this.snack.open('Generating…', undefined, { duration: 1500 });
    this.svc.generate(s.id).subscribe((report) => {
      this.dialog.open(ReportViewerComponent, {
        data: { html: report.renderedHtml },
        width: '620px',
      });
      this.loadReports();
    });
  }

  open(r: GeneratedReportSummary) {
    this.svc
      .getReport(r.id)
      .subscribe((d) =>
        this.dialog.open(ReportViewerComponent, { data: { html: d.renderedHtml }, width: '620px' }),
      );
  }

  remove(s: ReportSchedule) {
    if (!confirm(`Delete schedule "${s.name}"?`)) return;
    this.svc.deleteSchedule(s.id).subscribe(() => {
      this.loadSchedules();
      this.loadReports();
    });
  }
}
