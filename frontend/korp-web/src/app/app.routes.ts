import { Routes } from '@angular/router';

import { ProductsComponent } from './pages/products/products';
import { InvoiceListComponent } from './pages/invoices/invoice-list/invoice-list';
import { InvoiceFormComponent } from './pages/invoices/invoice-form/invoice-form';

export const routes: Routes = [
  {
    path: '',
    redirectTo: 'products',
    pathMatch: 'full',
  },
  {
    path: 'products',
    component: ProductsComponent,
    title: 'Produtos',
  },
  {
    path: 'invoices',
    component: InvoiceListComponent,
    title: 'Notas Fiscais',
  },
  {
    path: 'invoices/new',
    component: InvoiceFormComponent,
    title: 'Nova Nota Fiscal',
  },
  {
    path: '**',
    redirectTo: 'products',
  },
];
