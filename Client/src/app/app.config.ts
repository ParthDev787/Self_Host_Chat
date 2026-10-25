import {
  ApplicationConfig,
  provideBrowserGlobalErrorListeners,
  provideZoneChangeDetection,
} from '@angular/core';
import { provideRouter, withViewTransitions } from '@angular/router';
import { routes } from './app.routes';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { jwtInterceptor } from '../core/interceptor/jwt-interceptor';
import { errorInterceptor } from '../core/interceptor/error-interceptor';
import { loggingInterceptor } from '../core/interceptor/logging-interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideZoneChangeDetection(),
    provideRouter(routes, withViewTransitions()),
    provideHttpClient(withInterceptors([jwtInterceptor, errorInterceptor, loggingInterceptor])),
  ],
};
