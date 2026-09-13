export interface LookupItem { id: string; name: string; }
export interface UserItem { id: string; name: string; email: string; isActive: boolean; createdAt: string; groups: LookupItem[]; }
export interface SaveUserRequest { name: string; email: string; password?: string | null; isActive: boolean; roleIds: string[]; }
export interface RoleItem { id: string; name: string; description?: string; isSystem: boolean; createdAt: string; permissions: string[]; }
export interface SaveRoleRequest { name: string; description?: string | null; permissions: string[]; }
export interface PermissionItem { code: string; name: string; module: string; }
export interface AuditItem { id: number; occurredAt: string; userName?: string; action: string; entity: string; entityId?: string; details?: string; }
export interface CategoryItem { id: string; name: string; description?: string; isActive: boolean; createdAt: string; updatedAt: string; }
export interface SaveCategoryRequest { name: string; description?: string | null; isActive: boolean; }
export interface ProductItem { id: string; categoryId: string; categoryName: string; name: string; sku: string; barcode?: string; salePrice: number; costPrice: number; minimumStock: number; isActive: boolean; createdAt: string; updatedAt: string; rowVersion: string; }
export interface SaveProductRequest { categoryId: string; name: string; sku: string; barcode?: string | null; salePrice: number; costPrice: number; minimumStock: number; isActive: boolean; rowVersion?: string | null; }
