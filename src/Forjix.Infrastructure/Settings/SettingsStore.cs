using System.Text.Json;
using Forjix.Application.Abstractions.Settings;
using Forjix.Application.Abstractions.Tenancy;
using Forjix.Application.Common;
using Forjix.Application.Features.Settings;
using Forjix.Domain.Entities.Audit;
using Forjix.Domain.Entities.Master;
using Forjix.Domain.Enums;
using Forjix.Infrastructure.Persistence.Master;
using Forjix.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;

namespace Forjix.Infrastructure.Settings;

internal sealed class SettingsStore(ForjixMasterDbContext master, ITenantDatabaseResolver resolver, ITenantDbContextFactory contexts) : ISettingsStore
{
    public async Task<TenantSettingsView?> GetAsync(Guid id, CancellationToken ct)
    {
        var tenant = await master.Tenants.AsNoTracking().Include(x => x.Settings).SingleOrDefaultAsync(x => x.Id == id, ct);
        return tenant is null ? null : Map(tenant);
    }

    public async Task<TenantSettingsView> UpdateAsync(Guid id, Guid user, UpdateTenantSettingsRequest request, string correlation, string? ip, DateTimeOffset now, CancellationToken ct)
    {
        var affected = await master.Tenants.Where(x => x.Id == id).ExecuteUpdateAsync(update => update
            .SetProperty(x => x.Name, request.TradeName).SetProperty(x => x.Cnpj, request.Cnpj).SetProperty(x => x.UpdatedAt, now), ct);
        if (affected == 0) throw new ResourceNotFoundException("Empresa não encontrada.");
        await SetAsync(id, TenantSettingKeys.LegalName, request.LegalName, now, ct);
        await SetAsync(id, TenantSettingKeys.Phone, request.Phone, now, ct);
        await SetAsync(id, TenantSettingKeys.Email, request.Email, now, ct);
        await SetAsync(id, TenantSettingKeys.Address, request.Address, now, ct);
        await SetAsync(id, TenantSettingKeys.AllowNegativeStock, request.AllowNegativeStock.ToString(), now, ct);
        await SetAsync(id, TenantSettingKeys.Currency, request.Currency, now, ct);
        await SetAsync(id, TenantSettingKeys.TimeZone, request.TimeZone, now, ct);
        await Audit(id, user, correlation, ip, now, new { request.TradeName, request.Cnpj, request.AllowNegativeStock }, ct);
        return await GetAsync(id, ct) ?? throw new ResourceNotFoundException("Empresa não encontrada.");
    }

    public Task<string?> GetLogoKeyAsync(Guid id, CancellationToken ct) => master.TenantSettings.AsNoTracking()
        .Where(x => x.TenantId == id && x.Key == TenantSettingKeys.LogoKey).Select(x => x.Value).SingleOrDefaultAsync(ct);

    public async Task SetLogoKeyAsync(Guid id, Guid user, string key, string correlation, string? ip, DateTimeOffset now, CancellationToken ct)
    {
        await SetAsync(id, TenantSettingKeys.LogoKey, key, now, ct);
        await Audit(id, user, correlation, ip, now, new { LogoUpdated = true }, ct);
    }

    private async Task SetAsync(Guid tenantId, string key, string? value, DateTimeOffset now, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            await master.TenantSettings.Where(x => x.TenantId == tenantId && x.Key == key).ExecuteDeleteAsync(ct);
            return;
        }
        var clean = value.Trim();
        var affected = await master.TenantSettings.Where(x => x.TenantId == tenantId && x.Key == key).ExecuteUpdateAsync(update => update
            .SetProperty(x => x.Value, clean).SetProperty(x => x.UpdatedAt, now), ct);
        if (affected == 0)
        {
            master.TenantSettings.Add(new TenantSetting { Id = Guid.NewGuid(), TenantId = tenantId, Key = key, Value = clean, UpdatedAt = now });
            await master.SaveChangesAsync(ct);
        }
    }

    private async Task Audit(Guid tenantId, Guid user, string correlation, string? ip, DateTimeOffset now, object data, CancellationToken ct)
    {
        var tenant = await resolver.ResolveByTenantIdAsync(tenantId, ct) ?? throw new ResourceNotFoundException("Empresa não encontrada.");
        await using var db = contexts.Create(tenant);
        db.AuditLogs.Add(new AuditLog { UserId = user, Action = AuditAction.TenantSettingsUpdated, EntityName = "TenantSettings", EntityId = tenantId.ToString(), AfterData = JsonSerializer.Serialize(data), CorrelationId = correlation, IpAddress = ip, OccurredAt = now });
        await db.SaveChangesAsync(ct);
    }

    private static TenantSettingsView Map(Tenant tenant)
    {
        string? Value(string key) => tenant.Settings.FirstOrDefault(x => x.Key == key)?.Value;
        return new(tenant.Name, Value(TenantSettingKeys.LegalName), tenant.Cnpj, Value(TenantSettingKeys.Phone), Value(TenantSettingKeys.Email), Value(TenantSettingKeys.Address), bool.TryParse(Value(TenantSettingKeys.AllowNegativeStock), out var negative) && negative, Value(TenantSettingKeys.Currency) ?? "BRL", Value(TenantSettingKeys.TimeZone) ?? "America/Sao_Paulo", Value(TenantSettingKeys.LogoKey) is not null);
    }
}
