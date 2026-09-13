using Forjix.Application.Abstractions.Tenancy;
using Forjix.Application.Features.Sales;
using Forjix.Domain.Enums;

namespace Forjix.Application.Abstractions.Sales;

public sealed record SalesAuditContext(string CorrelationId, string? IpAddress);

public interface ISalesStoreFactory
{
    ISalesStore Create(ResolvedTenantDatabase tenant);
}

public interface ISalesStore : IAsyncDisposable
{
    Task<SaleView> CreateAsync(string idempotencyKey, Guid userId, PaymentMethod paymentMethod, decimal discount, Guid? customerId, IReadOnlyList<CreateSaleItemRequest> items, bool allowNegativeStock, SalesAuditContext audit, DateTimeOffset now, CancellationToken cancellationToken);
    Task<(List<SaleListItem> Items, int Total)> GetAsync(DateTimeOffset? from, DateTimeOffset? through, SaleStatus? status, int page, int pageSize, CancellationToken cancellationToken);
    Task<SaleView?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<SaleView> CancelAsync(Guid id, Guid userId, string reason, byte[] rowVersion, bool allowNegativeStock, SalesAuditContext audit, DateTimeOffset now, CancellationToken cancellationToken);
}
