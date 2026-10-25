import { HttpErrorResponse, HttpInterceptorFn, HttpRequest, HttpHandlerFn } from '@angular/common/http';
import { ToastService } from '../services/toast-service';
import { inject } from '@angular/core';
import { NavigationExtras, Router } from '@angular/router';
import { BehaviorSubject, catchError, filter, switchMap, take, throwError } from 'rxjs';
import { AccountService } from '../services/account-service';

// State shared across interceptor invocations for refresh token locking & queueing
let isRefreshing = false;
const refreshTokenSubject = new BehaviorSubject<string | null>(null);

export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const toast = inject(ToastService);
  const route = inject(Router);
  const accountService = inject(AccountService);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error) {
        switch (error.status) {
          case 400:
            if (error.error?.errors) {
              const modelStateErrors: string[] = [];
              for (const key in error.error.errors) {
                if (error.error.errors[key]) {
                  modelStateErrors.push(error.error.errors[key]);
                }
              }
              throw modelStateErrors.flat();
            } else {
              const msg = typeof error.error === 'string'
                ? error.error
                : (error.error?.title || error.statusText || 'Bad Request');
              toast.error(msg);
            }
            break;

          case 401:
            // 1. If 401 occurred on refresh-token endpoint itself, the 1-day refresh token expired
            if (req.url.includes('account/refresh-token')) {
              isRefreshing = false;
              refreshTokenSubject.next(null);
              const hadActiveUser = !!accountService.currentUser();
              accountService.logout();
              if (hadActiveUser) {
                toast.error('Session expired. Please log in again.');
              }
              route.navigateByUrl('/');
              break;
            }

            // 2. If 401 on login or register, just show unauthorized error
            if (req.url.includes('account/login') || req.url.includes('account/register')) {
              toast.error(typeof error.error === 'string' ? error.error : 'Invalid username or password');
              break;
            }

            // 3. 401 on protected resource -> Access Token (10 min) expired! Attempt automatic refresh
            return handle401Error(req, next, accountService, route, toast);

          case 404:
            route.navigateByUrl('/not-found');
            break;

          case 500:
            const navigationExtras: NavigationExtras = { state: { error: error.error } };
            route.navigateByUrl('/server-error', navigationExtras);
            break;

          default:
            toast.error('Something went wrong');
            break;
        }
      }
      return throwError(() => error);
    })
  );
};

function handle401Error(
  req: HttpRequest<unknown>,
  next: HttpHandlerFn,
  accountService: AccountService,
  route: Router,
  toast: ToastService
) {
  if (!isRefreshing) {
    isRefreshing = true;
    refreshTokenSubject.next(null);

    return accountService.refreshToken().pipe(
      switchMap((newUser) => {
        isRefreshing = false;
        refreshTokenSubject.next(newUser.token);

        // Retry the original request with the freshly acquired 10-minute access token
        const retriedReq = req.clone({
          setHeaders: {
            Authorization: `Bearer ${newUser.token}`
          },
          withCredentials: true
        });
        return next(retriedReq);
      }),
      catchError((refreshErr) => {
        isRefreshing = false;
        refreshTokenSubject.next(null);
        accountService.logout();
        toast.error('Session expired. Please log in again.');
        route.navigateByUrl('/');
        return throwError(() => refreshErr);
      })
    );
  } else {
    // If refresh is already in flight, queue concurrent requests until new token arrives
    return refreshTokenSubject.pipe(
      filter((token) => token !== null),
      take(1),
      switchMap((token) => {
        const retriedReq = req.clone({
          setHeaders: {
            Authorization: `Bearer ${token}`
          },
          withCredentials: true
        });
        return next(retriedReq);
      })
    );
  }
}
