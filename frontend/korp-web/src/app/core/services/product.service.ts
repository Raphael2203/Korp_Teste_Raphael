import { Injectable } from "@angular/core";
import { HttpClient } from "@angular/common/http";
import { Observable } from "rxjs";
import { Product } from "../../models/product";

@Injectable({
    providedIn: 'root'
})
export class ProductService {
    private readonly apiUrl = 'http://localhost:5159/api/Products';

    constructor(private http: HttpClient) {}

    getAll(): Observable<Product[]> {
        return this.http.get<Product[]>(this.apiUrl);
    }

    create(product: Omit<Product, 'id'>): Observable<Product> {
        return this.http.post<Product>(this.apiUrl, product);
    }
}