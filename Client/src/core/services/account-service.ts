import { HttpClient } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { map, Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { LoginCreds, RegisterCreds, User } from '../../types/user';

@Injectable({
  providedIn: 'root'
})
export class AccountService {
  private http = inject(HttpClient);
  private baseUrl = environment.apiUrl;

  currentUser = signal<User | null>(null);

  login(model: LoginCreds): Observable<User> {
    return this.http.post<User>(this.baseUrl + 'account/login', model, { withCredentials: true }).pipe(
      map(user => {
        if (user) {
          this.setCurrentUser(user);
        }
        return user;
      })
    );
  }

  register(model: RegisterCreds): Observable<User> {
    return this.http.post<User>(this.baseUrl + 'account/register', model, { withCredentials: true }).pipe(
      map(user => {
        if (user) {
          this.setCurrentUser(user);
        }
        return user;
      })
    );
  }

  refreshToken(): Observable<User> {
    return this.http.post<User>(this.baseUrl + 'account/refresh-token', {}, { withCredentials: true }).pipe(
      map(user => {
        if (user) {
          this.setCurrentUser(user);
        }
        return user;
      })
    );
  }

  setCurrentUser(user: User | null) {
    if (user && user.username && typeof user.username === 'string' && user.username.trim() && user.token) {
      localStorage.setItem('user', JSON.stringify(user));
      this.currentUser.set(user);
    } else {
      localStorage.removeItem('user');
      this.currentUser.set(null);
    }
  }

  logout() {
    this.http.post(this.baseUrl + 'account/logout', {}, { withCredentials: true }).subscribe({
      error: () => {}
    });
    localStorage.removeItem('user');
    this.currentUser.set(null);

    // Completely wipe all sensitive identity keys from both sessionStorage and localStorage
    try {
      for (let i = sessionStorage.length - 1; i >= 0; i--) {
        const k = sessionStorage.key(i);
        if (k?.startsWith('signal_identity_')) {
          sessionStorage.removeItem(k);
        }
      }
      for (let i = localStorage.length - 1; i >= 0; i--) {
        const k = localStorage.key(i);
        if (k?.startsWith('signal_identity_') || k === 'dating_app_requests') {
          localStorage.removeItem(k);
        }
      }
    } catch {}
  }
}
