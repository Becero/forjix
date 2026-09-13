export type PaymentMethod = 'Cash' | 'Pix' | 'CreditCard' | 'DebitCard';
export interface CreateSaleRequest { paymentMethod: PaymentMethod; discount: number; customerId?: string | null; items: { productId: string; quantity: number }[]; }
export interface SaleItem { id: string; productId: string; productName: string; sku: string; quantity: number; unitPrice: number; unitCost: number; discount: number; total: number; }
export interface Sale { id: string; number: string; status: 'Completed' | 'Cancelled'; subtotal: number; discount: number; total: number; paymentMethod: PaymentMethod; userId: string; userName: string; customerId?: string; createdAt: string; cancelledAt?: string; cancellationReason?: string; rowVersion: string; items: SaleItem[]; }
export interface SaleListItem { id: string; number: string; status: 'Completed' | 'Cancelled'; total: number; paymentMethod: PaymentMethod; userName: string; customerId?: string; itemCount: number; createdAt: string; rowVersion: string; }
export interface PagedSales { items: SaleListItem[]; page: number; pageSize: number; total: number; }
