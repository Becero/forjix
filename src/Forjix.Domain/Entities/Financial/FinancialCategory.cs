using Forjix.Domain.Enums;

namespace Forjix.Domain.Entities.Financial;

public sealed class FinancialCategory
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public FinancialCategoryType Type { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
