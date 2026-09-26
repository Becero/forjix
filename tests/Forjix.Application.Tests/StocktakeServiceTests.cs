using Forjix.Application.Abstractions.Identity;
using Forjix.Application.Abstractions.Inventory;
using Forjix.Application.Abstractions.Sales;
using Forjix.Application.Abstractions.Tenancy;
using Forjix.Application.Common;
using Forjix.Application.Features.Inventory;
namespace Forjix.Application.Tests;
public sealed class StocktakeServiceTests
{
    private static readonly string Version=Convert.ToBase64String(new byte[8]);
    private static StocktakeView View()=>new(Guid.NewGuid(),"INV-000001","Draft",null,Guid.NewGuid(),"Usuário",DateTimeOffset.UtcNow,null,null,Version,[]);
    [Fact] public async Task CreationResolvesAuthenticatedTenantAndCleansNotes()
    {
        var factory=new Factory();await Service(factory).SaveAsync(null,new([Guid.NewGuid()]," nota "));
        Assert.NotNull(factory.Tenant);Assert.Equal("nota",factory.Store.Saved!.Notes);
    }
    [Fact] public async Task InvalidCreationIsRejectedBeforeDatabase()
    {
        var factory=new Factory();var service=Service(factory);var id=Guid.NewGuid();
        await Assert.ThrowsAsync<RequestValidationException>(()=>service.SaveAsync(null,new([],"")));
        await Assert.ThrowsAsync<RequestValidationException>(()=>service.SaveAsync(null,new([id,id],"")));
        Assert.Null(factory.Tenant);
    }
    [Fact] public async Task EditingRequiresVersion()
    {
        await Assert.ThrowsAsync<RequestValidationException>(()=>Service(new Factory()).SaveAsync(Guid.NewGuid(),new([Guid.NewGuid()],null)));
    }
    [Fact] public async Task StartAndCompleteDelegateCurrentVersion()
    {
        var factory=new Factory();var service=Service(factory);
        await service.ActionAsync(Guid.NewGuid(),"start",new(Version));Assert.Equal("start",factory.Store.Action);
        await service.ActionAsync(Guid.NewGuid(),"complete",new(Version));Assert.Equal("complete",factory.Store.Action);
        factory.Store.Fail=true;await Assert.ThrowsAsync<ResourceConflictException>(()=>service.ActionAsync(Guid.NewGuid(),"complete",new(Version)));
    }
    [Fact] public async Task CountValidatesPrecisionAndSendsBatch()
    {
        var factory=new Factory();var service=Service(factory);
        await Assert.ThrowsAsync<RequestValidationException>(()=>service.CountAsync(Guid.NewGuid(),new(Version,[new(Guid.NewGuid(),.0001m,null)])));
        await service.CountAsync(Guid.NewGuid(),new(Version,[new(Guid.NewGuid(),0,null)]));Assert.NotNull(factory.Store.Counts);
    }
    [Fact] public async Task CancelRequiresBusinessReason()
    {
        await Assert.ThrowsAsync<RequestValidationException>(()=>Service(new Factory()).ActionAsync(Guid.NewGuid(),"cancel",new(Version)));
    }
    [Fact] public async Task ReportsValidateWindowAndOverviewIsTenantScoped()
    {
        var factory=new Factory();var service=Service(factory);await Assert.ThrowsAsync<RequestValidationException>(()=>service.ReportAsync(false,10,null,null));
        await service.ReportAsync(true,30,null,null);Assert.True(factory.Store.Low);
        var result=await service.OverviewAsync();Assert.Equal(2,result.LowStock);
    }
    [Fact] public async Task AdjustmentCannotSpoofInventoryOrigin()
    {
        await Assert.ThrowsAsync<RequestValidationException>(()=>Service(new Factory()).AdjustAsync(new(Guid.NewGuid(),"PositiveAdjustment",1,"Inventory",null,Version)));
    }
    private static StocktakeService Service(Factory f)=>new(new Resolver(),f,new Current(),TimeProvider.System,null!);
    private sealed class Current : ICurrentUser
    {
        public Guid? UserId {get;}=Guid.NewGuid();public Guid? TenantId {get;}=Guid.NewGuid();public string? TenantSlug=>"test";public bool IsAuthenticated=>true;public string CorrelationId=>"test";public string? IpAddress=>null;
    }
    private sealed class Resolver : ITenantDatabaseResolver
    {
        public Task<ResolvedTenantDatabase?> ResolveBySlugAsync(string slug,CancellationToken cancellationToken=default)=>throw new NotSupportedException();
        public Task<ResolvedTenantDatabase?> ResolveByTenantIdAsync(Guid id,CancellationToken cancellationToken=default)=>Task.FromResult<ResolvedTenantDatabase?>(new(id,"Empresa","test","isolated","","1",[],new Dictionary<string,string>()));
    }
    private sealed class Factory : IStocktakeStoreFactory
    {
        public ResolvedTenantDatabase? Tenant {get;private set;}public Store Store {get;}=new();
        public IStocktakeStore Create(ResolvedTenantDatabase tenant){Tenant=tenant;return Store;}
    }
    private sealed class Store : IStocktakeStore
    {
        public SaveStocktakeRequest? Saved {get;private set;}public CountStocktakeRequest? Counts {get;private set;}public string? Action {get;private set;}public bool Low {get;private set;}public bool Fail {get;set;}
        public Task<StocktakeView> SaveAsync(Guid? id,SaveStocktakeRequest request,Guid user,SalesAuditContext audit,DateTimeOffset now,CancellationToken ct){Saved=request;return Task.FromResult(View());}
        public Task<StocktakeView> ActionAsync(Guid id,string action,StocktakeActionRequest request,Guid user,SalesAuditContext audit,DateTimeOffset now,CancellationToken ct){if(Fail)throw new ResourceConflictException("Rollback");Action=action;return Task.FromResult(View());}
        public Task<StocktakeView> CountAsync(Guid id,CountStocktakeRequest request,Guid user,SalesAuditContext audit,DateTimeOffset now,CancellationToken ct){Counts=request;return Task.FromResult(View());}
        public Task<StockOverview> OverviewAsync(CancellationToken ct)=>Task.FromResult(new StockOverview(10,1,2,1,1,100));
        public Task<IReadOnlyList<StockReportItem>> ReportAsync(bool lowStock,DateTimeOffset from,DateTimeOffset toDate,CancellationToken ct){Low=lowStock;return Task.FromResult<IReadOnlyList<StockReportItem>>([]);}
        public Task<PagedResult<StockMovementView>> MovementsAsync(StockMovementFilter filter,CancellationToken ct)=>throw new NotSupportedException();
        public Task<PagedResult<StocktakeView>> ListAsync(StocktakeFilter filter,CancellationToken ct)=>throw new NotSupportedException();
        public Task<StocktakeView?> GetAsync(Guid id,CancellationToken ct)=>Task.FromResult<StocktakeView?>(View());
        public ValueTask DisposeAsync()=>ValueTask.CompletedTask;
    }
}
