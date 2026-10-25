import { Component, inject, OnInit } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { AccountService } from '../core/services/account-service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App implements OnInit {
  private accountService = inject(AccountService);

  ngOnInit(): void {
    try {
      const savedTheme = localStorage.getItem('theme') || 'dark';
      document.documentElement.setAttribute('data-theme', savedTheme);
    } catch {}
    this.setCurrentUser();
  }

  setCurrentUser() {
    const userString = localStorage.getItem('user');
    if (!userString || userString === 'null' || userString === 'undefined') {
      this.accountService.currentUser.set(null);
      return;
    }

    try {
      const user = JSON.parse(userString);
      if (!user || typeof user !== 'object' || !user.username || typeof user.username !== 'string' || !user.username.trim() || !user.token) {
        localStorage.removeItem('user');
        this.accountService.currentUser.set(null);
        return;
      }
      this.accountService.currentUser.set(user);
    } catch {
      localStorage.removeItem('user');
      this.accountService.currentUser.set(null);
    }
  }
}