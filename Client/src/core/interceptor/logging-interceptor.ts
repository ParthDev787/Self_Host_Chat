import { HttpInterceptorFn, HttpResponse, HttpErrorResponse } from '@angular/common/http';
import { tap } from 'rxjs';
import { environment } from '../../environments/environment';

// Auto-purge any legacy client logs or sensitive keys from localStorage
if (typeof window !== 'undefined') {
  try {
    localStorage.removeItem('dating_app_client_logs');
    localStorage.removeItem('dating_app_requests');
    for (let i = localStorage.length - 1; i >= 0; i--) {
      const k = localStorage.key(i);
      if (k?.startsWith('signal_identity_')) {
        localStorage.removeItem(k);
      }
    }
  } catch {}
}

export function sendLog(level: string, message: string, correlationId?: string) {
  try {
    // Resolve base API URL (e.g. https://localhost:5001 or current origin)
    let baseUrl = environment.apiUrl || '';
    if (baseUrl.endsWith('/api/')) {
      baseUrl = baseUrl.slice(0, -5);
    } else if (baseUrl.endsWith('/api')) {
      baseUrl = baseUrl.slice(0, -4);
    } else if (baseUrl.endsWith('/')) {
      baseUrl = baseUrl.slice(0, -1);
    }

    const logEndpoint = `${baseUrl || ''}/api/logs`;

    // Native fetch ensures this logging call does not re-trigger the Angular interceptor
    fetch(logEndpoint, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        level,
        message,
        correlationId: correlationId || 'none',
        url: typeof window !== 'undefined' ? window.location.href : '',
      }),
    }).catch(() => {});
  } catch {}
}

// Global JS error listeners to capture unhandled client errors into frontend-.log file
if (typeof window !== 'undefined' && !(window as any).__errorLogListenersRegistered) {
  (window as any).__errorLogListenersRegistered = true;
  window.addEventListener('error', (e) => sendLog('error', e.message));
  window.addEventListener('unhandledrejection', (e) => sendLog('error', String(e.reason)));
}

export const loggingInterceptor: HttpInterceptorFn = (req, next) => {
  const startTime = performance.now();
  const url = req.urlWithParams || req.url;

  // 1) Generate and attach X-Correlation-Id header to follow request from browser to server
  const correlationId = typeof crypto !== 'undefined' && crypto.randomUUID
    ? crypto.randomUUID().replace(/-/g, '')
    : Date.now().toString(36) + Math.random().toString(36).substring(2, 9);

  const reqWithCorrelation = req.clone({
    setHeaders: {
      'X-Correlation-Id': correlationId,
    },
  });

  return next(reqWithCorrelation).pipe(
    tap({
      next: (event) => {
        if (event instanceof HttpResponse) {
          const duration = Math.round(performance.now() - startTime);

          // Log response to server Logs/frontend-.log file with correlation ID
          sendLog(
            'info',
            `${req.method} ${url} -> ${event.status} (${duration}ms)`,
            correlationId
          );
        }
      },
      error: (error) => {
        const duration = Math.round(performance.now() - startTime);
        let errorMessage = '';
        if (error instanceof HttpErrorResponse) {
          errorMessage = typeof error.error === 'string'
            ? error.error
            : error.message || error.statusText;
        } else {
          errorMessage = String(error);
        }

        // Log error to server Logs/frontend-.log file with correlation ID
        sendLog(
          'error',
          `${url} failed: ${errorMessage} (${duration}ms)`,
          correlationId
        );
      },
    })
  );
};
