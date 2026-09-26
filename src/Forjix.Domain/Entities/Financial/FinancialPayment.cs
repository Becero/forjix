using Forjix.Domain.Entities.Cash;
using Forjix.Domain.Enums;

namespace Forjix.Domain.Entities.Financial;

public sealed class FinancialPayment
{
    public Guid Id { get; set; }
    public Guid? AccountReceivableId { get; set; }
    public Guid? AccountPayableId { get; set; }
    public FinancialOperationType Type { get; set; }
    public required string IdempotencyKey { get; set; }
    public decimal Amount { get; set; }
    public decimal PrincipalAmount { get; set; }
    public decimal Discount { get; set; }
    public decimal Interest { get; set; }
    public decimal Penalty { get; set; }
    public DateOnly PaymentDate { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public string? Notes { get; set; }
    public long? CashMovementId { get; set; }
    public long? ReversalCashMovementId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? ReversedByUserId { get; private set; }
    public DateTimeOffset? ReversedAt { get; private set; }
    public string? ReversalReason { get; private set; }
    public byte[] RowVersion { get; set; } = [];
    public AccountReceivable? AccountReceivable { get; set; }
    public AccountPayable? AccountPayable { get; set; }
    public CashMovement? CashMovement { get; set; }
    public CashMovement? ReversalCashMovement { get; set; }

    public void Reverse(Guid user, string reason, DateTimeOffset now)
    {
        if (ReversedAt.HasValue) throw new InvalidOperationException("Esta baixa já foi estornada.");
        ReversedByUserId = user;
        ReversalReason = reason;
        ReversedAt = now;
    }
}
