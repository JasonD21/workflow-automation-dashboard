import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSnackBar } from '@angular/material/snack-bar';
import { AutomationsService } from './automations.service';
import { CatalogService } from './catalog.service';
import { ConnectionsService } from '../connections/connections.service';
import {
  TriggerDefinition,
  ActionDefinition,
  Connection,
  SaveAutomation,
  FilterCondition,
} from '../core/models';

@Component({
  standalone: true,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatSlideToggleModule,
  ],
  template: ` <h1>{{ id ? 'Edit automation' : 'New automation' }}</h1>

    <form [formGroup]="form" (ngSubmit)="save()">
      <mat-card>
        <mat-form-field appearance="outline" class="full">
          <mat-label>Name</mat-label>
          <input matInput formControlName="name" placeholder="e.g. Invoice paid → notify finance" />
        </mat-form-field>
      </mat-card>

      <mat-card>
        <h3>When this happens</h3>
        <mat-form-field appearance="outline" class="full">
          <mat-label>Trigger</mat-label>
          <mat-select formControlName="triggerType" (selectionChange)="onTriggerType($event.value)">
            @for (t of triggers(); track t.type) {
              <mat-option [value]="t.type">{{ t.displayName }}</mat-option>
            }
          </mat-select>
        </mat-form-field>

        @if (selectedTrigger(); as tr) {
          <p class="desc">{{ tr.description }}</p>
          @if (triggerConnections().length) {
            <mat-form-field appearance="outline" class="full">
              <mat-label>{{ pretty(tr.provider) }} account</mat-label>
              <mat-select formControlName="triggerConnectionId">
                @for (c of triggerConnections(); track c.id) {
                  <mat-option [value]="c.id">{{ c.displayName }} ({{ c.status }})</mat-option>
                }
              </mat-select>
            </mat-form-field>
          } @else {
            <p class="warn">
              No {{ pretty(tr.provider) }} connection.
              <a routerLink="/connections">Connect one</a> first.
            </p>
          }

          <div formGroupName="triggerConfig">
            @for (f of tr.configFields; track f.key) {
              <mat-form-field appearance="outline" class="full">
                <mat-label>{{ f.label }}</mat-label>
                <input matInput [formControlName]="f.key" />
                @if (f.type === 'slack-channel') {
                  <mat-hint>Channel ID, e.g. C0123ABC</mat-hint>
                }
              </mat-form-field>
            }
          </div>
        }
      </mat-card>

      <mat-card>
        <mat-slide-toggle formControlName="hasFilter"
          >Only run if a condition is met</mat-slide-toggle
        >
        @if (form.controls.hasFilter.value && selectedTrigger()) {
          <div formGroupName="filter" class="filter">
            <mat-form-field appearance="outline">
              <mat-label>Field</mat-label>
              <mat-select formControlName="field">
                @for (tok of availableTokens(); track tok) {
                  <mat-option [value]="tok">{{ tok }}</mat-option>
                }
              </mat-select>
            </mat-form-field>
            <mat-form-field appearance="outline">
              <mat-label>Condition</mat-label>
              <mat-select formControlName="operator">
                <mat-option value="contains">contains</mat-option>
                <mat-option value="equals">equals</mat-option>
                <mat-option value="gte">≥</mat-option>
                <mat-option value="lte">≤</mat-option>
              </mat-select>
            </mat-form-field>
            <mat-form-field appearance="outline">
              <mat-label>Value</mat-label>
              <input matInput formControlName="value" />
            </mat-form-field>
          </div>
        }
      </mat-card>

      <mat-card>
        <h3>Do this</h3>
        <mat-form-field appearance="outline" class="full">
          <mat-label>Action</mat-label>
          <mat-select formControlName="actionType" (selectionChange)="onActionType($event.value)">
            @for (a of actions(); track a.type) {
              <mat-option [value]="a.type">{{ a.displayName }}</mat-option>
            }
          </mat-select>
        </mat-form-field>

        @if (selectedAction(); as act) {
          <p class="desc">{{ act.description }}</p>
          @if (act.requiresConnection) {
            @if (actionConnections().length) {
              <mat-form-field appearance="outline" class="full">
                <mat-label>{{ pretty(act.provider) }} account</mat-label>
                <mat-select formControlName="actionConnectionId">
                  @for (c of actionConnections(); track c.id) {
                    <mat-option [value]="c.id">{{ c.displayName }} ({{ c.status }})</mat-option>
                  }
                </mat-select>
              </mat-form-field>
            } @else {
              <p class="warn">
                No {{ pretty(act.provider) }} connection.
                <a routerLink="/connections">Connect one</a> first.
              </p>
            }
          }

          <div formGroupName="actionConfig">
            @for (f of act.configFields; track f.key) {
              <mat-form-field appearance="outline" class="full">
                <mat-label>{{ f.label }}</mat-label>
                @if (f.type === 'textarea') {
                  <textarea matInput rows="3" [formControlName]="f.key"></textarea>
                } @else {
                  <input matInput [type]="inputType(f.type)" [formControlName]="f.key" />
                }
                @if (f.type === 'slack-channel') {
                  <mat-hint>Channel ID, e.g. C0123ABC</mat-hint>
                }
              </mat-form-field>
              @if (act.templatedFields.includes(f.key) && availableTokens().length) {
                <div class="tokens">
                  <span>Insert:</span>
                  @for (tok of availableTokens(); track tok) {
                    <button type="button" class="chip" (click)="insertToken(f.key, tok)">
                      {{ '{{' + tok + '}}' }}
                    </button>
                  }
                </div>
              }
            }
          </div>
        }
      </mat-card>

      @if (errors().length) {
        <mat-card class="errors">
          @for (e of errors(); track e) {
            <div>{{ e }}</div>
          }
        </mat-card>
      }

      <div class="footer">
        <a mat-button routerLink="/automations">Cancel</a>
        <button mat-flat-button color="primary" [disabled]="saving()">Save automation</button>
      </div>
    </form>`,
  styles: [
    `
      form {
        max-width: 640px;
      }
      mat-card {
        padding: 16px 20px;
        margin-bottom: 16px;
      }
      h3 {
        margin-top: 0;
      }
      .full {
        width: 100%;
      }
      .desc {
        color: #666;
        margin: 0 0 12px;
      }
      .warn {
        color: #e65100;
      }
      .filter {
        display: grid;
        grid-template-columns: 1fr 1fr 1fr;
        gap: 8px;
        margin-top: 12px;
      }
      .tokens {
        display: flex;
        flex-wrap: wrap;
        align-items: center;
        gap: 6px;
        margin: -8px 0 12px;
        font-size: 12px;
        color: #888;
      }
      .chip {
        border: 1px solid #ddd;
        background: #f7f7f7;
        border-radius: 12px;
        padding: 2px 8px;
        cursor: pointer;
        font-family: monospace;
      }
      .errors {
        background: #ffebee;
        color: #c62828;
      }
      .footer {
        display: flex;
        justify-content: flex-end;
        gap: 8px;
      }
    `,
  ],
})
export class BuilderComponent implements OnInit {
  private fb = inject(FormBuilder);
  private svc = inject(AutomationsService);
  private catalog = inject(CatalogService);
  private connSvc = inject(ConnectionsService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private snack = inject(MatSnackBar);

  id = this.route.snapshot.paramMap.get('id');
  triggers = signal<TriggerDefinition[]>([]);
  actions = signal<ActionDefinition[]>([]);
  connections = signal<Connection[]>([]);
  selectedTrigger = signal<TriggerDefinition | null>(null);
  selectedAction = signal<ActionDefinition | null>(null);
  errors = signal<string[]>([]);
  saving = signal(false);

  triggerConnections = computed(() =>
    this.connections().filter((c) => c.provider === this.selectedTrigger()?.provider),
  );
  actionConnections = computed(() =>
    this.connections().filter((c) => c.provider === this.selectedAction()?.provider),
  );
  availableTokens = computed(() => this.selectedTrigger()?.tokens ?? []);

  form = this.fb.group({
    name: ['', Validators.required],
    triggerType: ['', Validators.required],
    triggerConnectionId: ['', Validators.required],
    triggerConfig: this.fb.group({}),
    hasFilter: [false],
    filter: this.fb.group({ field: [''], operator: ['contains'], value: [''] }),
    actionType: ['', Validators.required],
    actionConnectionId: [''],
    actionConfig: this.fb.group({}),
  });

  ngOnInit() {
    forkJoin({
      triggers: this.catalog.triggers(),
      actions: this.catalog.actions(),
      conns: this.connSvc.list(),
    }).subscribe(({ triggers, actions, conns }) => {
      this.triggers.set(triggers);
      this.actions.set(actions);
      this.connections.set(conns);
      if (this.id) this.loadForEdit(this.id);
    });
  }

  onTriggerType(type: string) {
    this.selectedTrigger.set(this.triggers().find((t) => t.type === type) ?? null);
    this.rebuild(this.form.controls.triggerConfig, this.selectedTrigger()?.configFields ?? []);
    this.form.controls.triggerConnectionId.setValue('');
  }

  onActionType(type: string) {
    const def = this.actions().find((a) => a.type === type) ?? null;
    this.selectedAction.set(def);
    this.rebuild(this.form.controls.actionConfig, def?.configFields ?? []);
    const conn = this.form.controls.actionConnectionId;
    if (def?.requiresConnection) conn.setValidators(Validators.required);
    else {
      conn.clearValidators();
      conn.setValue('');
    }
    conn.updateValueAndValidity();
  }

  insertToken(key: string, token: string) {
    const ctrl = this.form.controls.actionConfig.get(key);
    if (ctrl) ctrl.setValue(`${ctrl.value ?? ''}{{${token}}}`);
  }

  inputType(type: string) {
    return type === 'number' ? 'number' : type === 'email' ? 'email' : 'text';
  }
  pretty(p: string) {
    return p === 'GoogleCalendar' ? 'Google Calendar' : p;
  }

  save() {
    this.errors.set([]);
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.saving.set(true);

    const v = this.form.getRawValue();
    const filterVal = this.form.controls.filter.getRawValue() as FilterCondition;
    const payload: SaveAutomation = {
      name: v.name!,
      triggerType: v.triggerType!,
      triggerConnectionId: v.triggerConnectionId!,
      triggerConfig: this.groupValue(this.form.controls.triggerConfig),
      filter: v.hasFilter && filterVal.field ? filterVal : null,
      actionType: v.actionType!,
      actionConnectionId: this.selectedAction()?.requiresConnection
        ? v.actionConnectionId || null
        : null,
      actionConfig: this.groupValue(this.form.controls.actionConfig) ?? {},
    };

    const req = this.id ? this.svc.update(this.id, payload) : this.svc.create(payload);
    req.subscribe({
      next: () => {
        this.snack.open('Automation saved', 'OK', { duration: 2500 });
        this.router.navigate(['/automations']);
      },
      error: (e: HttpErrorResponse) => {
        this.saving.set(false);
        const errs = e.error?.errors;
        this.errors.set(errs ? (Object.values(errs).flat() as string[]) : ['Save failed.']);
      },
    });
  }

  private rebuild(group: FormGroup, fields: { key: string; required: boolean }[]) {
    Object.keys(group.controls).forEach((k) => group.removeControl(k));
    for (const f of fields)
      group.addControl(f.key, this.fb.control('', f.required ? Validators.required : []));
  }

  private groupValue(g: FormGroup): Record<string, string> | null {
    const val = g.getRawValue() as Record<string, string>;
    return Object.keys(val).length ? val : null;
  }

  private loadForEdit(id: string) {
    this.svc.get(id).subscribe((a) => {
      this.onTriggerType(a.triggerType); // build dynamic controls first…
      this.onActionType(a.actionType);
      this.form.patchValue({
        // …then fill them
        name: a.name,
        triggerType: a.triggerType,
        triggerConnectionId: a.triggerConnectionId,
        triggerConfig: a.triggerConfig ?? {},
        hasFilter: !!a.filter,
        filter: a.filter ?? { field: '', operator: 'contains', value: '' },
        actionType: a.actionType,
        actionConnectionId: a.actionConnectionId ?? '',
        actionConfig: a.actionConfig ?? {},
      });
    });
  }
}
