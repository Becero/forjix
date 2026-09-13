export type InventoryStatus = 'Normal' | 'Low' | 'OutOfStock' | 'Negative';
export type InventoryMovementType = 'StockEntry' | 'StockExit' | 'PositiveAdjustment' | 'NegativeAdjustment';
export interface InventoryItem { productId: string; productName: string; sku: string; barcode?: string; categoryId: string; categoryName: string; quantity: number; minimumStock: number; costPrice: number; salePrice: number; status: InventoryStatus; lastMovementAt?: string; rowVersion: string; }
export interface InventoryMovementItem { id: number; productId: string; type: InventoryMovementType; quantity: number; previousQuantity: number; newQuantity: number; reason?: string; referenceType?: string; referenceId?: string; userId: string; userName: string; createdAt: string; }
export interface CreateInventoryMovementRequest { type: InventoryMovementType; quantity: number; reason?: string | null; rowVersion?: string; }
export interface InventoryMovementResult { inventory: InventoryItem; movement: InventoryMovementItem; }
export interface PagedResult<T> { items: T[]; page: number; pageSize: number; total: number; }
