import { Routes } from '@angular/router';
import { Home } from '../features/home/home';

export const routes: Routes = [
    {
        path: '',
        redirectTo: 'Home',
        pathMatch: 'full'
    },
    {
        path: 'Home',
        component: Home
    }
];
