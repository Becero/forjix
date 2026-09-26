using Forjix.Domain.Entities.Quotes;
using Forjix.Domain.Enums;
namespace Forjix.Domain.Tests;
public sealed class QuoteTests
{
    private static DateOnly Today => new(2026, 9, 26);
    private static Quote Valid()
    {
        var q = new Quote { Id = Guid.NewGuid(), Number = "ORC-000001", CustomerId = Guid.NewGuid(), IssueDate = Today, ValidUntil = Today.AddDays(10), Discount = 2 };
        q.Items.Add(new QuoteItem { ProductId = Guid.NewGuid(), ProductName = "Produto", Sku = "P", Quantity = 2, UnitPrice = 10, Discount = 1 });
        q.Calculate(); return q;
    }
    [Fact] public void CalculatesFromItemsWithHeaderAndItemDiscounts()
    {
        var q = Valid(); Assert.Equal(20, q.Subtotal); Assert.Equal(17, q.Total); Assert.Equal(QuoteStatus.Draft, q.Status);
        Assert.Equal(19, q.Items.Single().Total);
    }
    [Theory]
    [InlineData(0, 10, 0)]
    [InlineData(-1, 10, 0)]
    [InlineData(1, -1, 0)]
    [InlineData(1, 10, 11)]
    [InlineData(1.0001, 10, 0)]
    public void RejectsInvalidItemValues(decimal quantity, decimal price, decimal discount)
    {
        var item = new QuoteItem { ProductName = "Produto", Sku = "P", Quantity = quantity, UnitPrice = price, Discount = discount };
        Assert.Throws<ArgumentException>(item.Calculate);
    }
    [Fact] public void RejectsDiscountGreaterThanNetItems()
    {
        var q = Valid(); q.Discount = 20; Assert.Throws<ArgumentException>(q.Calculate);
    }
    [Fact] public void ApproveRequiresSentAndConvertRequiresApproved()
    {
        var q = Valid(); Assert.Throws<InvalidOperationException>(() => q.Transition(QuoteStatus.Approved, Today));
        Assert.Throws<InvalidOperationException>(() => q.EnsureConvertible(Today));
        q.Transition(QuoteStatus.Sent, Today); q.Transition(QuoteStatus.Approved, Today);
        q.EnsureConvertible(Today); Assert.Throws<InvalidOperationException>(q.EnsureEditable);
    }
    [Fact] public void ConvertedQuoteCannotBeConvertedEditedOrCancelledAgain()
    {
        var q = Valid(); q.Transition(QuoteStatus.Sent, Today); q.Transition(QuoteStatus.Approved, Today);
        q.ConvertToSale(Guid.NewGuid(), new DateTimeOffset(Today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));
        Assert.Equal(QuoteStatus.Converted, q.Status); Assert.NotNull(q.SaleId);
        Assert.Throws<InvalidOperationException>(() => q.ConvertToSale(Guid.NewGuid(), DateTimeOffset.UtcNow));
        Assert.Throws<InvalidOperationException>(q.Calculate);
        Assert.Throws<InvalidOperationException>(() => q.Transition(QuoteStatus.Cancelled, Today));
    }
    [Fact] public void ExpirationDoesNotOverrideTerminalStates()
    {
        var q = Valid(); q.Transition(QuoteStatus.Sent, Today); q.Transition(QuoteStatus.Rejected, Today);
        Assert.Equal(QuoteStatus.Rejected, q.StatusOn(Today.AddDays(20)));
        Assert.Throws<InvalidOperationException>(() => q.EnsureConvertible(Today));
        var expired = Valid(); Assert.Equal(QuoteStatus.Expired, expired.StatusOn(Today.AddDays(20)));
        Assert.Throws<InvalidOperationException>(() => expired.Transition(QuoteStatus.Sent, Today.AddDays(20)));
        expired.Transition(QuoteStatus.Cancelled, Today.AddDays(20));
        Assert.Equal(QuoteStatus.Cancelled, expired.StatusOn(Today.AddDays(20)));
    }
    [Fact] public void FractionalQuantitiesRoundEachItemToCents()
    {
        var q = Valid(); q.Items.Clear(); q.Discount = 0;
        q.Items.Add(new QuoteItem { ProductName = "Fracionado", Sku = "F", ProductId = Guid.NewGuid(), Quantity = 0.333m, UnitPrice = 1.01m });
        q.Calculate(); Assert.Equal(0.34m, q.Total);
    }
}
