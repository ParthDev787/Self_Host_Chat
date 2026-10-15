import { Component, inject, signal } from '@angular/core';
import { Register } from '../account/register/register';
import { AccountService } from '../../core/services/account-service';

@Component({
  selector: 'app-home',
  imports: [Register],
  templateUrl: './home.html',
  styleUrl: './home.css'
})
export class Home {
  protected accountService = inject(AccountService);
  registerMode = signal(false);

  registerToggle() {
    this.registerMode.set(!this.registerMode());
  }

  cancelRegisterMode(event: boolean) {
    this.registerMode.set(event);
  }
}
