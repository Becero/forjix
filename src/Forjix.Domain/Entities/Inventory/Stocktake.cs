using Forjix.Domain.Enums;
namespace Forjix.Domain.Entities.Inventory;

public sealed class Stocktake
{
    public Guid Id { get; set; }
    public required string Number { get; set; }
    public StocktakeStatus Status { get; private set; } = StocktakeStatus.Draft;
    public string? Notes { get; set; }
    public Guid UserId { get; set; }
    public Guid UpdatedByUserId { get; set; }
    public DateTimeOffset OpenedAt { get; set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public ICollection<StocktakeItem> Items { get; set; } = [];
    public void EnsureDraft() { if (Status != StocktakeStatus.Draft) throw new InvalidOperationException("Somente inventários em rascunho podem ser editados."); }
    public void EnsureCounting() { if (Status != StocktakeStatus.Counting) throw new InvalidOperationException("Inventário não está em contagem."); }
    public void Start(DateTimeOffset now)
    {
        EnsureDraft();
        if (Items.Count == 0) throw new InvalidOperationException("Selecione ao menos um produto.");
        Status = StocktakeStatus.Counting; StartedAt = now;
    }
    public void Complete(DateTimeOffset now)
    {
        EnsureCounting();
        if (Items.Count == 0 || Items.Any(x => x.CountedQuantity is null)) throw new InvalidOperationException("Todos os produtos precisam ser contados.");
        Status = StocktakeStatus.Completed; ClosedAt = now;
    }
    public void Cancel(DateTimeOffset now)
    {
        if (Status is not (StocktakeStatus.Draft or StocktakeStatus.Counting)) throw new InvalidOperationException("Inventário encerrado não pode ser cancelado.");
        Status = StocktakeStatus.Cancelled; ClosedAt = now;
    }
}
public sealed class StocktakeItem
{
    public Guid Id { get; set; }
    public Guid StocktakeId { get; set; }
    public Guid ProductId { get; set; }
    public decimal ExpectedQuantity { get; set; }
    public decimal? CountedQuantity { get; private set; }
    public decimal? Difference => CountedQuantity - ExpectedQuantity;
    public string? Notes { get; private set; }
    public DateTimeOffset? CountedAt { get; private set; }
    public Guid? CountedByUserId { get; private set; }
    public byte[] StockRowVersion { get; set; } = [];
    public Stocktake Stocktake { get; set; } = null!;
    public Catalog.Product Product { get; set; } = null!;
    public void Count(decimal quantity, string? notes, Guid user, DateTimeOffset now)
    {
        ValidateQuantity(quantity);
        if (user == Guid.Empty || notes?.Length > 500) throw new ArgumentException("Contagem inválida.");
        CountedQuantity = quantity; Notes = notes?.Trim(); CountedAt = now; CountedByUserId = user;
    }
    public static void ValidateQuantity(decimal quantity)
    {
        if (quantity < 0 || quantity > 999999999999999.999m || decimal.Round(quantity, 3) != quantity) throw new ArgumentException("Quantidade deve ser não negativa com até três casas decimais.");
    }
}
public sealed class StocktakeSequence
{
    public int Id { get; set; } = 1;
    public long LastValue { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
