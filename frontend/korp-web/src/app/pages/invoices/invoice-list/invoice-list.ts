import { Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs';

import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

import { Invoice } from '../../../models/invoice';
import { InvoiceService } from '../../../core/services/invoice.service';
import { NotificationService } from '../../../core/services/notification.service';

@Component({
  selector: 'app-invoice-list',
  imports: [
    DatePipe,
    RouterLink,
    MatButtonModule,
    MatIconModule,
    MatCardModule,
    MatTableModule,
    MatChipsModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './invoice-list.html',
  styleUrl: './invoice-list.css',
})
export class InvoiceListComponent implements OnInit {
  private readonly invoiceService = inject(InvoiceService);
  private readonly notifications = inject(NotificationService);

  readonly invoices = signal<Invoice[]>([]);
  readonly loading = signal(false);

  /** Id da nota sendo impressa no momento (controla o indicador na linha). */
  readonly printingId = signal<number | null>(null);

  readonly displayedColumns: string[] = ['number', 'status', 'createdAt', 'items', 'actions'];

  /**
   * ngOnInit: carrega as notas assim que a tela abre.
   */
  ngOnInit(): void {
    this.loadInvoices();
  }

  loadInvoices(): void {
    this.loading.set(true);

    this.invoiceService
      .getAll()
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (invoices) => this.invoices.set(invoices),
      });
  }

  /**
   * Aciona a impressão da nota. O `finalize` garante que o indicador de
   * processamento seja encerrado tanto no sucesso quanto na falha — inclusive
   * quando o serviço de estoque está fora do ar.
   */
  print(invoice: Invoice): void {
    this.printingId.set(invoice.id);

    this.invoiceService
      .print(invoice.id)
      .pipe(finalize(() => this.printingId.set(null)))
      .subscribe({
        next: (printed) => {
          this.notifications.success(`Nota ${printed.number} impressa. Estoque atualizado.`);

          this.replaceInvoice(printed);
        },
        error: () => {
          // A nota continua Aberta no backend; recarrega para refletir o
          // estado real após a falha.
          this.loadInvoices();
        },
      });
  }

  totalItems(invoice: Invoice): number {
    return invoice.items.reduce((total, item) => total + item.quantity, 0);
  }

  translateStatus(invoice: Invoice): string {
    return invoice.status === 'Open' ? 'Aberta' : 'Fechada';
  }

  private replaceInvoice(updated: Invoice): void {
    this.invoices.update((invoices) =>
      invoices.map((invoice) => (invoice.id === updated.id ? updated : invoice)),
    );
  }
}
