using Forjix.Domain.Enums;

namespace Forjix.Domain.Entities.Financial;

public abstract class FinancialAccount
{
    public Guid Id { get; set; }
    public Guid GroupId { get; set; }
    public Guid FinancialCategoryId { get; set; }
    public required string Description { get; set; }
    public string? Document { get; set; }
    public decimal OriginalAmount { get; private set; }
    public decimal OpenAmount { get; private set; }
    public DateOnly IssueDate { get; set; }
    public DateOnly DueDate { get; set; }
    public DateOnly? PaymentDate { get; private set; }
    public FinancialAccountStatus Status { get; private set; } = FinancialAccountStatus.Pending;
    public int InstallmentNumber { get; set; }
    public int TotalInstallments { get; set; }
    public string? Notes { get; set; }
    public Guid CreatedByUserId { get; set; }
    public Guid UpdatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public FinancialCategory FinancialCategory { get; set; } = null!;

    public void SetAmount(decimal amount)
    {
        FinancialRules.PositiveMoney(amount);
        if (Status == FinancialAccountStatus.Cancelled || OpenAmount != OriginalAmount)
            throw new InvalidOperationException("Não é possível alterar o valor de uma conta com baixas ou cancelada.");
        OriginalAmount = OpenAmount = amount;
    }

    public FinancialAccountStatus StatusOn(DateOnly today) =>
        Status != FinancialAccountStatus.Cancelled && OpenAmount > 0 && DueDate < today
            ? FinancialAccountStatus.Overdue : Status;

    public void Pay(decimal principal, DateOnly date)
    {
        FinancialRules.PositiveMoney(principal);
        if (Status is FinancialAccountStatus.Cancelled or FinancialAccountStatus.Paid || principal > OpenAmount)
            throw new InvalidOperationException("Baixa incompatível com o saldo da conta.");
        OpenAmount -= principal;
        Status = OpenAmount == 0 ? FinancialAccountStatus.Paid : FinancialAccountStatus.PartiallyPaid;
        PaymentDate = OpenAmount == 0 ? date : null;
    }

    public void Reverse(decimal principal)
    {
        FinancialRules.PositiveMoney(principal);
        if (Status == FinancialAccountStatus.Cancelled || OpenAmount + principal > OriginalAmount)
            throw new InvalidOperationException("Estorno incompatível com o saldo da conta.");
        OpenAmount += principal;
        Status = OpenAmount == OriginalAmount ? FinancialAccountStatus.Pending : FinancialAccountStatus.PartiallyPaid;
        PaymentDate = null;
    }

    public void Cancel()
    {
        if (Status == FinancialAccountStatus.Cancelled || OpenAmount != OriginalAmount)
            throw new InvalidOperationException("Estorne as baixas antes de cancelar a conta.");
        Status = FinancialAccountStatus.Cancelled;
    }
}

public static class FinancialRules
{
    public static void PositiveMoney(decimal value)
    {
        if (value <= 0 || value > 9999999999999999.99m || decimal.Round(value, 2) != value)
            throw new ArgumentException("Informe um valor positivo com até duas casas decimais.");
    }

    public static decimal Principal(decimal amount, decimal discount, decimal interest, decimal penalty)
    {
        PositiveMoney(amount);
        if (new[] { discount, interest, penalty }.Any(x => x < 0 || x > 9999999999999999.99m || decimal.Round(x, 2) != x))
            throw new ArgumentException("Desconto, juros e multa devem ser não negativos com até duas casas decimais.");
        var principal = amount + discount - interest - penalty;
        PositiveMoney(principal);
        return principal;
    }

    public static IReadOnlyList<decimal> Split(decimal total, int installments)
    {
        PositiveMoney(total);
        if (installments is < 1 or > 120 || total * 100 < installments)
            throw new ArgumentException("Informe de 1 a 120 parcelas, cada uma de pelo menos um centavo.");
        var cents = total * 100;
        var baseCents = decimal.Floor(cents / installments);
        var remainder = (int)(cents - baseCents * installments);
        return Enumerable.Range(0, installments).Select(i => (baseCents + (i < remainder ? 1 : 0)) / 100).ToArray();
    }
}
