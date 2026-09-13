using Forjix.Application.Common;

namespace Forjix.Application.Tests;

public sealed class TenantSettingKeysTests
{
    [Fact]
    public void NegativeStockSettingHasStableKey()
    {
        Assert.Equal("AllowNegativeStock", TenantSettingKeys.AllowNegativeStock);
        Assert.False(TenantSettingDefaults.AllowNegativeStock);
    }
}
