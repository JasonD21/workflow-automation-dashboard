import { Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { forkJoin } from 'rxjs';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatIconModule } from '@angular/material/icon';
import { RunsService } from './runs.service';
import { AutomationsService } from '../automations/automations.service';
import { Run } from '../core/models';

@Component({
  standalone: true,
  imports: [DatePipe, MatExpansionModule, MatButtonToggleModule, MatPaginatorModule, MatIconModule],
  template: ` <h1>Activity</h1>

    <mat-button-toggle-group [value]="status() ?? 'all'" (change)="setStatus($event.value)">
      <mat-button-toggle value="all">All</mat-button-toggle>
      <mat-button-toggle value="Success">Success</mat-button-toggle>
      <mat-button-toggle value="Failed">Failed</mat-button-toggle>
      <mat-button-toggle value="Skipped">Skipped</mat-button-toggle>
    </mat-button-toggle-group>

    @if (loading()) {
      <p>Loading…</p>
    } @else if (!runs().length) {
      <p class="muted">No runs match.</p>
    } @else {
      <mat-accordion class="list">
        @for (r of runs(); track r.id) {
          <mat-expansion-panel>
            <mat-expansion-panel-header>
              <mat-panel-title>
                <span
                  class="dot"
                  [class.ok]="r.status === 'Success'"
                  [class.bad]="r.status === 'Failed'"
                  [class.skip]="r.status === 'Skipped'"
                ></span>
                {{ name(r.automationId) }}
                @if (r.isTest) {
                  <span class="test">test</span>
                }
              </mat-panel-title>
              <mat-panel-description
                >{{ r.triggeredAt | date: 'medium' }} · {{ r.status }}</mat-panel-description
              >
            </mat-expansion-panel-header>

            <div class="detail">
              @if (payloadEntries(r).length) {
                <h4>Trigger data</h4>
                @for (e of payloadEntries(r); track e.k) {
                  <div class="kv">
                    <code>{{ e.k }}</code
                    ><span>{{ e.v }}</span>
                  </div>
                }
              }
              @if (resultText(r)) {
                <h4>Result</h4>
                <p>{{ resultText(r) }}</p>
              }
              @if (r.errorMessage) {
                <h4>Error</h4>
                <p class="err">{{ r.errorMessage }}</p>
              }
              <p class="muted">Took {{ r.durationMs ?? 0 }} ms</p>
            </div>
          </mat-expansion-panel>
        }
      </mat-accordion>

      <mat-paginator
        [length]="total()"
        [pageSize]="pageSize"
        [pageIndex]="page() - 1"
        [pageSizeOptions]="[10, 20, 50]"
        (page)="onPage($event)"
      />
    }`,
  styles: [
    `
      mat-button-toggle-group {
        margin-bottom: 16px;
      }
      .list {
        display: block;
      }
      .dot {
        width: 8px;
        height: 8px;
        border-radius: 50%;
        background: #bbb;
        display: inline-block;
        margin-right: 8px;
      }
      .dot.ok {
        background: #2e7d32;
      }
      .dot.bad {
        background: #c62828;
      }
      .dot.skip {
        background: #f9a825;
      }
      .test {
        font-size: 11px;
        background: #eee;
        border-radius: 8px;
        padding: 1px 6px;
        margin-left: 8px;
        color: #777;
      }
      .detail {
        padding: 4px 0;
      }
      h4 {
        margin: 12px 0 4px;
        font-size: 12px;
        text-transform: uppercase;
        color: #888;
      }
      .kv {
        display: flex;
        gap: 12px;
        padding: 2px 0;
      }
      .kv code {
        min-width: 160px;
        color: #555;
      }
      .err {
        color: #c62828;
      }
      .muted {
        color: #999;
      }
    `,
  ],
})
export class ActivityComponent implements OnInit {
  private svc = inject(RunsService);
  private autos = inject(AutomationsService);

  runs = signal<Run[]>([]);
  total = signal(0);
  page = signal(1);
  status = signal<string | null>(null);
  loading = signal(true);
  pageSize = 20;
  private names = new Map<string, string>();

  ngOnInit() {
    forkJoin({ list: this.autos.list(), runs: this.svc.list(null, 1, this.pageSize) }).subscribe(
      ({ list, runs }) => {
        list.forEach((a) => this.names.set(a.id, a.name));
        this.apply(runs);
      },
    );
  }

  setStatus(value: string) {
    this.status.set(value === 'all' ? null : value);
    this.page.set(1);
    this.load();
  }
  onPage(e: PageEvent) {
    this.page.set(e.pageIndex + 1);
    this.pageSize = e.pageSize;
    this.load();
  }

  private load() {
    this.loading.set(true);
    this.svc.list(this.status(), this.page(), this.pageSize).subscribe((r) => this.apply(r));
  }
  private apply(r: { items: Run[]; total: number }) {
    this.runs.set(r.items);
    this.total.set(r.total);
    this.loading.set(false);
  }

  name(id: string) {
    return this.names.get(id) ?? '(deleted automation)';
  }

  payloadEntries(r: Run) {
    try {
      const o = JSON.parse(r.triggerPayloadSummary ?? '{}');
      return Object.entries(o).map(([k, v]) => ({ k, v: String(v) }));
    } catch {
      return [];
    }
  }
  resultText(r: Run) {
    try {
      const o = JSON.parse(r.actionResultSummary ?? '{}');
      return o.summary ?? o.reason ?? '';
    } catch {
      return r.actionResultSummary ?? '';
    }
  }
}
