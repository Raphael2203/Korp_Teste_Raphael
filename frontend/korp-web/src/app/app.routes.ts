import { Routes } from '@angular/router';
import { ProductsComponent } from './pages/products/products';

export const routes: Routes = [
  {
    path: '',
    redirectTo: 'products',
    pathMatch: 'full'
  },
  {
    path: 'products',
    component: ProductsComponent
  }
];