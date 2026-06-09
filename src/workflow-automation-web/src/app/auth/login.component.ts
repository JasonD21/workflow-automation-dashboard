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
      <h1>Sign in</h1>
      <form [formGroup]="form" (ngSubmit)="submit()">
        <mat-form-field appearance="outline">
          <mat-label>Email</mat-label>
          <input matInput type="email" formControlName="email" />
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>Password</mat-label>
          <input matInput type="password" formControlName="password" />
        </mat-form-field>
        @if (error()) {
          <p class="error">{{ error() }}</p>
        }
        <button mat-flat-button color="primary" [disabled]="form.invalid || loading()">
          Sign in
        </button>
      </form>
      <p>No account? <a routerLink="/register">Create one</a></p>
      <div class="divider">or</div>
      <button mat-stroked-button type="button" (click)="demo()" [disabled]="loading()">
        Explore the demo
      </button>
    </mat-card>
  </div>`,
  styles: [
    `
      .divider {
        text-align: center;
        color: #999;
        margin: 8px 0;
      }
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
export class LoginComponent {
  private fb = inject(FormBuilder);
  private auth = inject(AuthService);
  private router = inject(Router);

  loading = signal(false);
  error = signal<string | null>(null);
  form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required],
  });

  demo() {
    this.loading.set(true);
    this.error.set(null);
    this.auth.demoLogin().subscribe({
      next: () => this.router.navigate(['/dashboard']),
      error: () => {
        this.error.set('Demo is unavailable right now.');
        this.loading.set(false);
      },
    });
  }

  submit() {
    if (this.form.invalid) return;
    this.loading.set(true);
    this.error.set(null);
    const { email, password } = this.form.getRawValue();
    this.auth.login(email, password).subscribe({
      next: () => this.router.navigate(['/dashboard']),
      error: () => {
        this.error.set('Invalid email or password.');
        this.loading.set(false);
      },
    });
  }
}
