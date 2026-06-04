import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { AuthService } from '../core/auth.service';

@Component({
  standalone: true,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
  ],
  template: ` <div class="auth-wrap">
    <mat-card class="auth-card">
      <h1>Create account</h1>
      <form [formGroup]="form" (ngSubmit)="submit()">
        <mat-form-field appearance="outline">
          <mat-label>Name</mat-label>
          <input matInput formControlName="displayName" />
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>Email</mat-label>
          <input matInput type="email" formControlName="email" />
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>Password</mat-label>
          <input matInput type="password" formControlName="password" />
          <mat-hint>At least 8 characters</mat-hint>
        </mat-form-field>
        @if (error()) {
          <p class="error">{{ error() }}</p>
        }
        <button mat-flat-button color="primary" [disabled]="form.invalid || loading()">
          Create account
        </button>
      </form>
      <p>Already have an account? <a routerLink="/login">Sign in</a></p>
    </mat-card>
  </div>`,
  styles: [
    `
      .auth-wrap {
        display: flex;
        justify-content: center;
        align-items: center;
        min-height: 100vh;
      }
      .auth-card {
        width: 360px;
        padding: 24px;
        display: flex;
        flex-direction: column;
        gap: 8px;
      }
      form {
        display: flex;
        flex-direction: column;
      }
      .error {
        color: #c00;
        margin: 0 0 8px;
      }
    `,
  ],
})
export class RegisterComponent {
  private fb = inject(FormBuilder);
  private auth = inject(AuthService);
  private router = inject(Router);

  loading = signal(false);
  error = signal<string | null>(null);
  form = this.fb.nonNullable.group({
    displayName: ['', Validators.required],
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(8)]],
  });

  submit() {
    if (this.form.invalid) return;
    this.loading.set(true);
    this.error.set(null);
    const { email, password, displayName } = this.form.getRawValue();
    this.auth.register(email, password, displayName).subscribe({
      next: () => this.router.navigate(['/dashboard']),
      error: (e) => {
        this.error.set(
          e?.error?.detail ?? 'Could not create the account. The email may already be in use.',
        );
        this.loading.set(false);
      },
    });
  }
}
