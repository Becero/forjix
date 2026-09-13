using Forjix.Domain.Entities.Sales;
using Forjix.Domain.Enums;

namespace Forjix.Domain.Tests;

public sealed class SaleTests
{
    [Fact]
    public void CompletedSaleCanBeCancelledOnce()
    {
        var sale = Sale(); var now = DateTimeOffset.UtcNow; var user = Guid.NewGuid();
        sale.Cancel(user, "Solicitação do cliente", now);
        Assert.Equal(SaleStatus.Cancelled, sale.Status);
        Assert.Equal(user, sale.CancelledByUserId);
        Assert.Equal(now, sale.CancelledAt);
        Assert.Throws<InvalidOperationException>(() => sale.Cancel(user, "Novamente", now));
    }

    private static Sale Sale() => new() { Id = Guid.NewGuid(), Number = "VD-20260913-00001", IdempotencyKey = Guid.NewGuid().ToString(), UserId = Guid.NewGuid(), CreatedAt = DateTimeOffset.UtcNow };
}
