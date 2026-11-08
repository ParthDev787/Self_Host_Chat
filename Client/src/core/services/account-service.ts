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
      if (user.theme && typeof localStorage !== 'undefined' && typeof document !== 'undefined') {
        localStorage.setItem('theme', user.theme);
        localStorage.setItem(`theme_${user.username.trim().toLowerCase()}`, user.theme);
        document.documentElement.setAttribute('data-theme', user.theme);
      }
      localStorage.setItem('user', JSON.stringify(user));
      this.currentUser.set(user);
    } else {
      localStorage.removeItem('user');
      this.currentUser.set(null);
    }
  }

  updateTheme(username: string, theme: string) {
    return this.http.put<{ message: string; theme: string }>(
      this.baseUrl + 'account/theme',
      { username, theme },
      { withCredentials: true }
    );
  }

  logout() {
    this.http.post(this.baseUrl + 'account/logout', {}, { withCredentials: true }).subscribe({
      error: () => {}
    });
    localStorage.removeItem('user');
    this.currentUser.set(null);
  }
}
