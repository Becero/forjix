namespace Forjix.Application.Features.Customers;

public sealed record CustomerItem(Guid Id, string Name, string? Document, string? Email, string? Phone, string? Notes, bool IsActive, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public sealed record SaveCustomerRequest(string Name, string? Document, string? Email, string? Phone, string? Notes, bool IsActive);
public sealed record PagedCustomers(IReadOnlyList<CustomerItem> Items, int Page, int PageSize, int Total);
