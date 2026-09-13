using Forjix.Application.Abstractions.Customers;
using Forjix.Application.Abstractions.Tenancy;
using Forjix.Application.Common;
using Forjix.Application.Features.Customers;
using Forjix.Domain.Entities.Audit;
using Forjix.Domain.Entities.Customers;
using Forjix.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;

namespace Forjix.Infrastructure.Customers;

internal sealed class CustomerStoreFactory(ITenantDbContextFactory contextFactory) : ICustomerStoreFactory { public ICustomerStore Create(ResolvedTenantDatabase tenant) => new CustomerStore(contextFactory.Create(tenant)); }
internal sealed class CustomerStore(TenantDbContext db) : ICustomerStore
{
    public async Task<(List<CustomerItem> Items, int Total)> GetAsync(string? search, bool? isActive, int page, int pageSize, CancellationToken cancellationToken) { var query = db.Customers.AsNoTracking().AsQueryable(); if (!string.IsNullOrWhiteSpace(search)) query = query.Where(x => x.Name.Contains(search) || (x.Document != null && x.Document.Contains(search)) || (x.Email != null && x.Email.Contains(search))); if (isActive.HasValue) query = query.Where(x => x.IsActive == isActive); var total = await query.CountAsync(cancellationToken); var items = await query.OrderBy(x => x.Name).Skip((page - 1) * pageSize).Take(pageSize).Select(x => new CustomerItem(x.Id, x.Name, x.Document, x.Email, x.Phone, x.Notes, x.IsActive, x.CreatedAt, x.UpdatedAt)).ToListAsync(cancellationToken); return (items, total); }
    public Task<bool> DocumentExistsAsync(string document, Guid? exceptId, CancellationToken cancellationToken) => db.Customers.AnyAsync(x => x.Document == document && (!exceptId.HasValue || x.Id != exceptId), cancellationToken);
    public Task<Customer?> FindAsync(Guid id, CancellationToken cancellationToken) => db.Customers.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    public async Task<CustomerItem> SaveAsync(Customer customer, AuditLog audit, CancellationToken cancellationToken) { if (db.Entry(customer).State == EntityState.Detached) db.Customers.Add(customer); db.AuditLogs.Add(audit); try { await db.SaveChangesAsync(cancellationToken); return Map(customer); } catch (DbUpdateException exception) { throw new ResourceConflictException(exception.InnerException is null ? "Não foi possível salvar o cliente." : "Documento de cliente já cadastrado."); } }
    private static CustomerItem Map(Customer x) => new(x.Id, x.Name, x.Document, x.Email, x.Phone, x.Notes, x.IsActive, x.CreatedAt, x.UpdatedAt);
    public ValueTask DisposeAsync() => db.DisposeAsync();
}
