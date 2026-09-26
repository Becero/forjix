using Forjix.Domain.Entities.Financial;
using Forjix.Domain.Enums;

namespace Forjix.Domain.Tests;

public sealed class FinancialAccountTests
{
    private static readonly decimal[] ExpectedSplit = [33.34m, 33.33m, 33.33m];
    [Fact]
    public void PartialAndFullPaymentsThenReversalMaintainPrincipal()
    {
        var account = Account(1200);
        account.Pay(400, Today);
        Assert.Equal(800, account.OpenAmount);
        Assert.Equal(FinancialAccountStatus.PartiallyPaid, account.Status);
        account.Pay(800, Today);
        Assert.Equal(0, account.OpenAmount);
        Assert.Equal(FinancialAccountStatus.Paid, account.Status);
        Assert.Equal(Today, account.PaymentDate);
        account.Reverse(800);
        Assert.Equal(800, account.OpenAmount);
        Assert.Null(account.PaymentDate);
    }
    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(100.001)]
    public void RejectsInvalidMoney(decimal amount) => Assert.Throws<ArgumentException>(() => Account(amount));
    [Fact]
    public void OverpaymentAndCancelledAccountCannotBePaid()
    {
        var account = Account(100);
        Assert.Throws<InvalidOperationException>(() => account.Pay(101, Today));
        account.Cancel();
        Assert.Throws<InvalidOperationException>(() => account.Pay(10, Today));
        Assert.Equal(100, account.OpenAmount);
        Assert.Equal(FinancialAccountStatus.Cancelled, account.StatusOn(Today.AddDays(2)));
    }
    [Fact]
    public void PaidAccountRejectsAnotherPaymentAndPartialAccountRejectsCancellation()
    {
        var account = Account(100);
        account.Pay(10, Today);
        Assert.Throws<InvalidOperationException>(account.Cancel);
        account.Pay(90, Today);
        Assert.Throws<InvalidOperationException>(() => account.Pay(1, Today));
    }
    [Fact]
    public void OverdueIsDerivedFromDueDateAndOpenBalance()
    {
        var account = Account(100);
        Assert.Equal(FinancialAccountStatus.Pending, account.StatusOn(Today));
        Assert.Equal(FinancialAccountStatus.Overdue, account.StatusOn(Today.AddDays(1)));
        account.Pay(100, Today);
        Assert.Equal(FinancialAccountStatus.Paid, account.StatusOn(Today.AddDays(1)));
    }
    [Fact]
    public void InstallmentsPreserveEveryCent()
    {
        Assert.Equal(ExpectedSplit, FinancialRules.Split(100, 3));
        Assert.Equal(1200, FinancialRules.Split(1200, 3).Sum());
        Assert.Throws<ArgumentException>(() => FinancialRules.Split(0.01m, 2));
    }
    [Fact]
    public void DiscountAndChargesSeparateCashFromPrincipal()
    {
        Assert.Equal(100, FinancialRules.Principal(105, 5, 8, 2));
        Assert.Throws<ArgumentException>(() => FinancialRules.Principal(10, 0, 20, 0));
    }
    private static DateOnly Today => new(2026, 9, 26);
    private static AccountReceivable Account(decimal amount)
    {
        var account = new AccountReceivable { Description = "Teste", DueDate = Today, IssueDate = Today, InstallmentNumber = 1, TotalInstallments = 1 };
        account.SetAmount(amount); return account;
    }
}
