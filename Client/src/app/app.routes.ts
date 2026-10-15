import { Routes } from '@angular/router';
import { Home } from '../features/home/home';
import { NotFound } from '../shared/error/not-found/not-found';
import { ServerError } from '../shared/error/server-error/server-error';

export const routes: Routes = [
  { path: '', component: Home },
  { path: 'not-found', component: NotFound },
  { path: 'server-error', component: ServerError },
  { path: '**', component: NotFound },
];
