namespace Forjix.Application.Features.Customers;

public interface ICustomerService
{
    Task<PagedCustomers> GetAsync(string? search, bool? isActive, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<CustomerItem> CreateAsync(SaveCustomerRequest request, CancellationToken cancellationToken = default);
    Task<CustomerItem> UpdateAsync(Guid id, SaveCustomerRequest request, CancellationToken cancellationToken = default);
}
