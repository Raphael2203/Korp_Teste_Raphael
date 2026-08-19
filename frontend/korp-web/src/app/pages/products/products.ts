import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';

import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { MatCardModule } from '@angular/material/card';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';

import { Product } from '../../models/product';
import { ProductService } from '../../core/services/product.service';
import { NotificationService } from '../../core/services/notification.service';

@Component({
  selector: 'app-products',
  imports: [
    ReactiveFormsModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatTableModule,
    MatCardModule,
    MatProgressSpinnerModule,
    MatTooltipModule,
  ],
  templateUrl: './products.html',
  styleUrl: './products.css',
})
export class ProductsComponent implements OnInit {
  private readonly productService = inject(ProductService);
  private readonly notifications = inject(NotificationService);
  private readonly formBuilder = inject(FormBuilder);

  readonly products = signal<Product[]>([]);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly suggesting = signal(false);

  readonly displayedColumns: string[] = ['code', 'description', 'stock'];

  readonly form = this.formBuilder.nonNullable.group({
    code: ['', [Validators.required, Validators.maxLength(50)]],
    description: ['', [Validators.required, Validators.maxLength(200)]],
    stock: [0, [Validators.required, Validators.min(0)]],
  });

  /**
   * ngOnInit: carrega a lista assim que o componente entra em tela.
   */
  ngOnInit(): void {
    this.loadProducts();
  }

  loadProducts(): void {
    this.loading.set(true);

    this.productService
      .getAll()
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (products) => this.products.set(products),
      });
  }

  createProduct(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);

    this.productService
      .create(this.form.getRawValue())
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: (product) => {
          this.notifications.success(`Produto ${product.code} cadastrado.`);
          this.form.reset({ code: '', description: '', stock: 0 });
          this.loadProducts();
        },
      });
  }

  /**
   * Funcionalidade opcional de IA: preenche a descrição a partir do rascunho.
   */
  suggestDescription(): void {
    const code = this.form.controls.code.value.trim();
    const draft = this.form.controls.description.value.trim();

    if (!code || !draft) {
      this.notifications.error('Informe o código e um rascunho da descrição para usar a sugestão.');
      return;
    }

    this.suggesting.set(true);

    this.productService
      .suggestDescription(code, draft)
      .pipe(finalize(() => this.suggesting.set(false)))
      .subscribe({
        next: (result) => {
          this.form.controls.description.setValue(result.suggestion);
          this.notifications.success('Descrição sugerida pela IA.');
        },
      });
  }
}
