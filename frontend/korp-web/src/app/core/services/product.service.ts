import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

import { environment } from '../../../environments/environment';
import { Product } from '../../models/product';

export interface DescriptionSuggestion {
  suggestion: string;
}

@Injectable({
  providedIn: 'root',
})
export class ProductService {
  private readonly apiUrl = `${environment.inventoryApiUrl}/api/Products`;

  private readonly http = inject(HttpClient);

  getAll(): Observable<Product[]> {
    return this.http.get<Product[]>(this.apiUrl);
  }

  create(product: Omit<Product, 'id'>): Observable<Product> {
    return this.http.post<Product>(this.apiUrl, product);
  }

  /**
   * Funcionalidade opcional de IA: sugere uma descrição comercial a partir do
   * código e de um rascunho. Responde 503 quando a IA não está configurada.
   */
  suggestDescription(code: string, draft: string): Observable<DescriptionSuggestion> {
    return this.http.post<DescriptionSuggestion>(`${this.apiUrl}/description-suggestion`, {
      code,
      draft,
    });
  }
}
