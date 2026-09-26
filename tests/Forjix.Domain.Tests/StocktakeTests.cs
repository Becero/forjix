using Forjix.Domain.Entities.Inventory;
using Forjix.Domain.Enums;
namespace Forjix.Domain.Tests;
public sealed class StocktakeTests
{
    private static Stocktake New() => new() { Id = Guid.NewGuid(), Number = "INV-000001", Items = [new() { Id = Guid.NewGuid(), ProductId = Guid.NewGuid(), ExpectedQuantity = 10 }] };
    [Theory][InlineData(8,-2)][InlineData(13,3)][InlineData(10,0)][InlineData(0,-10)]
    public void CountsCalculateDifferenceWithoutChangingExpected(decimal counted,decimal delta)
    {
        var item = New().Items.Single(); item.Count(counted,"nota",Guid.NewGuid(),DateTimeOffset.UtcNow);
        Assert.Equal(delta,item.Difference); Assert.Equal(10,item.ExpectedQuantity); Assert.NotNull(item.CountedAt);
    }
    [Theory][InlineData(-1)][InlineData(.0001)]
    public void InvalidCountIsRejected(decimal quantity) => Assert.Throws<ArgumentException>(() => New().Items.Single().Count(quantity,null,Guid.NewGuid(),DateTimeOffset.UtcNow));
    [Fact] public void StateFlowCompletesOnceAndLocksChanges()
    {
        var item = New(); var now = DateTimeOffset.UtcNow;
        Assert.Equal(StocktakeStatus.Draft,item.Status); item.Start(now);
        item.Items.Single().Count(8,null,Guid.NewGuid(),now); item.Complete(now);
        Assert.Equal(StocktakeStatus.Completed,item.Status); Assert.Equal(now,item.ClosedAt);
        Assert.Throws<InvalidOperationException>(()=>item.Complete(now)); Assert.Throws<InvalidOperationException>(()=>item.Cancel(now)); Assert.Throws<InvalidOperationException>(item.EnsureDraft);
    }
    [Fact] public void MissingCountsAndInvalidTransitionsAreRejected()
    {
        var item=New(); Assert.Throws<InvalidOperationException>(()=>item.Complete(DateTimeOffset.UtcNow));
        item.Start(DateTimeOffset.UtcNow); Assert.Throws<InvalidOperationException>(()=>item.Start(DateTimeOffset.UtcNow)); Assert.Throws<InvalidOperationException>(()=>item.Complete(DateTimeOffset.UtcNow));
    }
    [Fact] public void CancellationDoesNotGenerateOrAlterCounts()
    {
        var item=New();item.Cancel(DateTimeOffset.UtcNow);Assert.Equal(StocktakeStatus.Cancelled,item.Status);Assert.Null(item.Items.Single().CountedQuantity);
    }
    [Theory][InlineData(InventoryMovementType.PositiveAdjustment,3,13)][InlineData(InventoryMovementType.NegativeAdjustment,2,8)]
    public void AdjustmentsReuseExistingStockDomain(InventoryMovementType type,decimal quantity,decimal expected)
    {
        var stock=Inventory.Create(Guid.NewGuid(),DateTimeOffset.UtcNow);stock.ApplyMovement(InventoryMovementType.StockEntry,10,false,DateTimeOffset.UtcNow);
        var delta=stock.ApplyMovement(type,quantity,false,DateTimeOffset.UtcNow);Assert.Equal(expected,delta.NewQuantity);Assert.Equal(10,delta.PreviousQuantity);
    }
}
