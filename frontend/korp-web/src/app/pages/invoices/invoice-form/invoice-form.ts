import { Component, OnInit, computed, inject, signal } from '@angular/core';
import {
  FormArray,
  FormBuilder,
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { Router } from '@angular/router';
import { finalize } from 'rxjs';

import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

import { Product } from '../../../models/product';
import { ProductService } from '../../../core/services/product.service';
import { InvoiceService } from '../../../core/services/invoice.service';
import { NotificationService } from '../../../core/services/notification.service';

/** Um item já adicionado à nota. */
type InvoiceItemForm = FormGroup<{
  productId: FormControl<number>;
  code: FormControl<string>;
  description: FormControl<string>;
  quantity: FormControl<number>;
}>;

@Component({
  selector: 'app-invoice-form',
  imports: [
    ReactiveFormsModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatCardModule,
    MatTableModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './invoice-form.html',
  styleUrl: './invoice-form.css',
})
export class InvoiceFormComponent implements OnInit {
  private readonly productService = inject(ProductService);
  private readonly invoiceService = inject(InvoiceService);
  private readonly notifications = inject(NotificationService);
  private readonly formBuilder = inject(FormBuilder);
  private readonly router = inject(Router);

  readonly products = signal<Product[]>([]);
  readonly loadingProducts = signal(false);
  readonly saving = signal(false);

  /**
   * Espelho dos itens do FormArray usado pela tabela e pelos cálculos.
   * O FormArray continua sendo a fonte de verdade do formulário.
   */
  readonly addedItems = signal<InvoiceItemForm[]>([]);

  readonly displayedColumns: string[] = ['code', 'description', 'quantity', 'actions'];

  /** Linha de entrada usada para montar cada item antes de adicioná-lo. */
  readonly itemForm = this.formBuilder.nonNullable.group({
    productId: [0, [Validators.required, Validators.min(1)]],
    quantity: [1, [Validators.required, Validators.min(1)]],
  });

  /** Itens da nota. */
  readonly items = new FormArray<InvoiceItemForm>([]);

  /** Produtos ainda não incluídos na nota (evita duplicidade). */
  readonly availableProducts = computed(() => {
    const chosen = this.addedItems().map((item) => item.getRawValue().productId);

    return this.products().filter((product) => !chosen.includes(product.id));
  });

  /**
   * ngOnInit: carrega os produtos disponíveis para seleção.
   */
  ngOnInit(): void {
    this.loadProducts();
  }

  loadProducts(): void {
    this.loadingProducts.set(true);

    this.productService
      .getAll()
      .pipe(finalize(() => this.loadingProducts.set(false)))
      .subscribe({
        next: (products) => this.products.set(products),
      });
  }

  addItem(): void {
    if (this.itemForm.invalid) {
      this.itemForm.markAllAsTouched();
      return;
    }

    const { productId, quantity } = this.itemForm.getRawValue();

    const product = this.products().find((candidate) => candidate.id === productId);

    if (!product) {
      this.notifications.error('Selecione um produto válido.');
      return;
    }

    if (quantity > product.stock) {
      this.notifications.error(
        `O produto ${product.code} possui apenas ${product.stock} em estoque.`,
      );
      return;
    }

    this.items.push(this.buildItem(product, quantity));
    this.addedItems.set([...this.items.controls]);

    this.itemForm.reset({ productId: 0, quantity: 1 });
  }

  removeItem(index: number): void {
    this.items.removeAt(index);
    this.addedItems.set([...this.items.controls]);
  }

  save(): void {
    if (this.items.length === 0) {
      this.notifications.error('Adicione ao menos um produto à nota.');
      return;
    }

    this.saving.set(true);

    const payload = {
      items: this.items.controls.map((control) => {
        const { productId, quantity } = control.getRawValue();
        return { productId, quantity };
      }),
    };

    this.invoiceService
      .create(payload)
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: (invoice) => {
          this.notifications.success(`Nota ${invoice.number} criada com status Aberta.`);

          this.router.navigate(['/invoices']);
        },
      });
  }

  cancel(): void {
    this.router.navigate(['/invoices']);
  }

  private buildItem(product: Product, quantity: number): InvoiceItemForm {
    return this.formBuilder.nonNullable.group({
      productId: product.id,
      code: product.code,
      description: product.description,
      quantity: quantity,
    });
  }
}
