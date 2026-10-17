import { HttpClient } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { map } from 'rxjs';
import { environment } from '../../environments/environment';
import { LoginCreds, RegisterCreds, User } from '../../types/user';

@Injectable({
  providedIn: 'root'
})
export class AccountService {
  private http = inject(HttpClient);
  private baseUrl = environment.apiUrl;

  currentUser = signal<User | null>(null);

  login(model: LoginCreds) {
    return this.http.post<User>(this.baseUrl + 'account/login', model).pipe(
      map(user => {
        if (user) {
          this.setCurrentUser(user);
        }
        return user;
      })
    );
  }

  register(model: RegisterCreds) {
    return this.http.post<User>(this.baseUrl + 'account/register', model).pipe(
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
    localStorage.removeItem('user');
    this.currentUser.set(null);
  }
}
