using Forjix.Application.Abstractions.Tenancy;
using Forjix.Application.Features.Customers;
using Forjix.Domain.Entities.Audit;
using Forjix.Domain.Entities.Customers;

namespace Forjix.Application.Abstractions.Customers;

public interface ICustomerStoreFactory { ICustomerStore Create(ResolvedTenantDatabase tenant); }
public interface ICustomerStore : IAsyncDisposable
{
    Task<(List<CustomerItem> Items, int Total)> GetAsync(string? search, bool? isActive, int page, int pageSize, CancellationToken cancellationToken);
    Task<bool> DocumentExistsAsync(string document, Guid? exceptId, CancellationToken cancellationToken);
    Task<Customer?> FindAsync(Guid id, CancellationToken cancellationToken);
    Task<CustomerItem> SaveAsync(Customer customer, AuditLog audit, CancellationToken cancellationToken);
}
