export type InvoiceStatus = 'Open' | 'Closed';

export interface InvoiceItem {
  id: number;
  productId: number;
  productCode: string;
  productDescription: string;
  quantity: number;
}

export interface Invoice {
  id: number;
  number: number;
  status: InvoiceStatus;
  createdAt: string;
  closedAt: string | null;
  items: InvoiceItem[];
}

export interface CreateInvoiceItem {
  productId: number;
  quantity: number;
}

export interface CreateInvoice {
  items: CreateInvoiceItem[];
}
