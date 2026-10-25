import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { AccountService } from '../services/account-service';
import { environment } from '../../environments/environment';

export const jwtInterceptor: HttpInterceptorFn = (req, next) => {
  const accountService = inject(AccountService);
  const user = accountService.currentUser();
  const isApiUrl = req.url.startsWith(environment.apiUrl);

  // Always enable credentials for API requests so HTTP-Only cookies are sent
  let headers = req.headers;
  if (user?.token && isApiUrl) {
    headers = headers.set('Authorization', `Bearer ${user.token}`);
  }

  const clonedReq = req.clone({
    headers,
    withCredentials: isApiUrl ? true : req.withCredentials,
  });

  return next(clonedReq);
};
