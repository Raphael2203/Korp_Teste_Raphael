import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatTableModule } from '@angular/material/table';

import { Product } from '../../models/product';
import { ProductService } from '../../core/services/product.service';

@Component({
  selector: 'app-products',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatTableModule
  ],
  templateUrl: './products.html',
  styleUrl: './products.css'
})
export class ProductsComponent implements OnInit {

  products: Product[] = [];

  displayedColumns: string[] = [
    'code',
    'description',
    'stock'
  ];

  newProduct = {
    code: '',
    description: '',
    stock: 0
  };

  constructor(private productService: ProductService) {}

  ngOnInit(): void {
    this.loadProducts();
  }

  loadProducts(): void {
    this.productService.getAll().subscribe({
      next: (products) => {
        this.products = products;
      },
      error: (error) => {
        console.error('Erro ao carregar produtos:', error);
      }
    });
  }

  createProduct(): void {
    this.productService.create(this.newProduct).subscribe({
      next: () => {
        this.newProduct = {
          code: '',
          description: '',
          stock: 0
        };

        this.loadProducts();
      },
      error: (error) => {
        console.error('Erro ao cadastrar produto:', error);
      }
    });
  }
}