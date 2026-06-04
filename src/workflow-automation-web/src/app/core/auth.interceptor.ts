import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from './auth.service';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const noBearer = ['/auth/login', '/auth/register', '/auth/refresh'].some((p) =>
    req.url.includes(p),
  );
  const token = auth.accessToken();

  const authed =
    token && !noBearer ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req;

  return next(authed).pipe(
    catchError((err: HttpErrorResponse) => {
      if (err.status === 401 && !noBearer) {
        return auth.refresh().pipe(
          switchMap((fresh) =>
            next(req.clone({ setHeaders: { Authorization: `Bearer ${fresh}` } })),
          ),
          catchError((e) => {
            auth.clearSession();
            return throwError(() => e);
          }),
        );
      }
      return throwError(() => err);
    }),
  );
};
