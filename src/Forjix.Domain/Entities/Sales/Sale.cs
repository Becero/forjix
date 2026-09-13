using Forjix.Domain.Entities.Identity;
using Forjix.Domain.Enums;

namespace Forjix.Domain.Entities.Sales;

public sealed class Sale
{
    public Guid Id { get; set; }
    public required string Number { get; set; }
    public required string IdempotencyKey { get; set; }
    public SaleStatus Status { get; private set; } = SaleStatus.Completed;
    public decimal Subtotal { get; set; }
    public decimal Discount { get; set; }
    public decimal Total { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public Guid UserId { get; set; }
    public Guid? CustomerId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CancelledAt { get; private set; }
    public Guid? CancelledByUserId { get; private set; }
    public string? CancellationReason { get; private set; }
    public byte[] RowVersion { get; set; } = [];
    public User User { get; set; } = null!;
    public ICollection<SaleItem> Items { get; set; } = [];

    public void Cancel(Guid userId, string reason, DateTimeOffset now)
    {
        if (Status == SaleStatus.Cancelled) throw new InvalidOperationException("Sale is already cancelled.");
        Status = SaleStatus.Cancelled;
        CancelledByUserId = userId;
        CancelledAt = now;
        CancellationReason = reason;
    }
}
