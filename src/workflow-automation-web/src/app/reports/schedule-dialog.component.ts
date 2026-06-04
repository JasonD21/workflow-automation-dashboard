import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatButtonModule } from '@angular/material/button';
import { ReportsService } from './reports.service';
import { ReportSchedule, SaveReportSchedule } from '../core/models';

@Component({
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatCheckboxModule,
    MatSlideToggleModule,
    MatButtonModule,
  ],
  template: ` <h2 mat-dialog-title>{{ data ? 'Edit schedule' : 'New schedule' }}</h2>
    <mat-dialog-content [formGroup]="form">
      <mat-form-field appearance="outline" class="full">
        <mat-label>Name</mat-label><input matInput formControlName="name" />
      </mat-form-field>

      <div class="when">
        <mat-form-field appearance="outline">
          <mat-label>Day</mat-label>
          <mat-select formControlName="dayOfWeek">
            @for (d of days; track d) {
              <mat-option [value]="d">{{ d }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>Time</mat-label><input matInput type="time" formControlName="time" />
        </mat-form-field>
      </div>

      <mat-form-field appearance="outline" class="full">
        <mat-label>Time zone</mat-label><input matInput formControlName="timeZone" />
      </mat-form-field>

      <label class="lbl">Include data from</label>
      @for (s of allSources; track s.key) {
        <mat-checkbox
          [checked]="selected().has(s.key)"
          (change)="toggleSource(s.key, $event.checked)"
          >{{ s.label }}</mat-checkbox
        >
      }

      <mat-form-field appearance="outline" class="full">
        <mat-label>Send to (blank = your email)</mat-label
        ><input matInput type="email" formControlName="recipientEmail" />
      </mat-form-field>

      <mat-slide-toggle formControlName="isEnabled">Enabled</mat-slide-toggle>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Cancel</button>
      <button
        mat-flat-button
        color="primary"
        [disabled]="form.invalid || saving()"
        (click)="save()"
      >
        Save
      </button>
    </mat-dialog-actions>`,
  styles: [
    `
      .full {
        width: 100%;
      }
      .when {
        display: flex;
        gap: 12px;
      }
      .when mat-form-field {
        flex: 1;
      }
      .lbl {
        display: block;
        margin: 4px 0;
        color: #666;
      }
      mat-checkbox {
        display: block;
        margin: 2px 0;
      }
    `,
  ],
})
export class ScheduleDialogComponent {
  private fb = inject(FormBuilder);
  private svc = inject(ReportsService);
  private ref = inject(MatDialogRef<ScheduleDialogComponent>);
  data = inject<ReportSchedule | null>(MAT_DIALOG_DATA);

  days = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];
  allSources = [
    { key: 'QuickBooks', label: 'QuickBooks' },
    { key: 'GoogleCalendar', label: 'Google Calendar' },
    { key: 'Slack', label: 'Slack' },
  ];
  selected = signal(
    new Set(this.data?.includedSources ?? ['QuickBooks', 'GoogleCalendar', 'Slack']),
  );
  saving = signal(false);

  form = this.fb.group({
    name: [this.data?.name ?? 'Weekly digest', Validators.required],
    dayOfWeek: [this.data?.dayOfWeek ?? 'Monday', Validators.required],
    time: [(this.data?.timeOfDay ?? '09:00:00').substring(0, 5), Validators.required],
    timeZone: [
      this.data?.timeZone ?? Intl.DateTimeFormat().resolvedOptions().timeZone,
      Validators.required,
    ],
    recipientEmail: [this.data?.recipientEmail ?? ''],
    isEnabled: [this.data?.isEnabled ?? true],
  });

  toggleSource(key: string, checked: boolean) {
    this.selected.update((s) => {
      const n = new Set(s);
      checked ? n.add(key) : n.delete(key);
      return n;
    });
  }

  save() {
    if (this.form.invalid) return;
    this.saving.set(true);
    const v = this.form.getRawValue();
    const payload: SaveReportSchedule = {
      name: v.name!,
      isEnabled: v.isEnabled!,
      dayOfWeek: v.dayOfWeek!,
      timeOfDay: `${v.time}:00`,
      timeZone: v.timeZone!,
      includedSources: [...this.selected()],
      recipientEmail: v.recipientEmail || null,
    };
    const req = this.data
      ? this.svc.updateSchedule(this.data.id, payload)
      : this.svc.createSchedule(payload);
    req.subscribe({ next: () => this.ref.close(true), error: () => this.saving.set(false) });
  }
}
