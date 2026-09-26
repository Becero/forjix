using System.Data;
using System.Text.Json;
using Forjix.Application.Abstractions.Quotes;
using Forjix.Application.Abstractions.Sales;
using Forjix.Application.Abstractions.Tenancy;
using Forjix.Application.Common;
using Forjix.Application.Features.Quotes;
using Forjix.Application.Features.Sales;
using Forjix.Domain.Entities.Audit;
using Forjix.Domain.Entities.Quotes;
using Forjix.Domain.Enums;
using Forjix.Infrastructure.Persistence.Tenant;
using Forjix.Infrastructure.Sales;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
namespace Forjix.Infrastructure.Quotes;
internal sealed class QuoteStoreFactory(ITenantDbContextFactory contexts) : IQuoteStoreFactory
{
    public IQuoteStore Create(ResolvedTenantDatabase tenant) => new QuoteStore(contexts.Create(tenant));
}
internal sealed class QuoteStore(TenantDbContext db) : IQuoteStore
{
    private IQueryable<Quote> Query() => db.Quotes.Include(x => x.Customer).Include(x => x.User).Include(x => x.Items);
    public async Task<QuoteView?> GetAsync(Guid id, DateOnly today, CancellationToken ct)
    {
        var quote = await Query().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        return quote is null ? null : Map(quote, today);
    }
    public async Task<PagedQuotes> ListAsync(QuoteFilter f, DateOnly today, CancellationToken ct)
    {
        var q = Query().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(f.Search)) q = q.Where(x => x.Number.Contains(f.Search));
        if (f.CustomerId.HasValue) q = q.Where(x => x.CustomerId == f.CustomerId);
        if (f.From.HasValue) q = q.Where(x => x.IssueDate >= f.From);
        if (f.Through.HasValue) q = q.Where(x => x.IssueDate <= f.Through);
        if (f.ConvertedOnly) q = q.Where(x => x.Status == QuoteStatus.Converted);
        if (f.ExpiredOnly || f.Status == "Expired") q = q.Where(x => (x.Status == QuoteStatus.Draft || x.Status == QuoteStatus.Sent || x.Status == QuoteStatus.Approved) && x.ValidUntil < today);
        else if (!string.IsNullOrWhiteSpace(f.Status))
        {
            var status = Enum.Parse<QuoteStatus>(f.Status);
            q = q.Where(x => x.Status == status && (x.Status == QuoteStatus.Rejected || x.Status == QuoteStatus.Converted || x.Status == QuoteStatus.Cancelled || x.ValidUntil >= today));
        }
        var count = await q.CountAsync(ct);
        var data = await q.OrderByDescending(x => x.IssueDate).ThenByDescending(x => x.Number).Skip((f.Page - 1) * f.PageSize).Take(f.PageSize).ToListAsync(ct);
        return new(data.Select(x => Map(x, today)).ToArray(), f.Page, f.PageSize, count);
    }
    public Task<QuoteView> SaveAsync(Guid? id, SaveQuoteRequest request, Guid user, SalesAuditContext audit, DateTimeOffset now, CancellationToken ct) => Write(async () =>
    {
        var customer = await db.Customers.SingleOrDefaultAsync(x => x.Id == request.CustomerId && x.IsActive, ct) ?? throw new RequestValidationException("Cliente inexistente ou inativo.");
        Quote quote;
        if (id.HasValue)
        {
            quote = await Query().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new ResourceNotFoundException("Orçamento não encontrado.");
            quote.EnsureEditable(); db.Entry(quote).Property(x => x.RowVersion).OriginalValue = Convert.FromBase64String(request.RowVersion!);
        }
        else
        {
            var sequence = await db.QuoteSequences.SingleOrDefaultAsync(x => x.Id == 1, ct);
            if (sequence is null) { sequence = new QuoteSequence(); db.QuoteSequences.Add(sequence); }
            sequence.LastValue++;
            quote = new Quote { Id = Guid.NewGuid(), Number = $"ORC-{sequence.LastValue:000000}", IssueDate = DateOnly.FromDateTime(now.UtcDateTime), UserId = user, CreatedAt = now };
            db.Quotes.Add(quote);
        }
        var ids = request.Items.Select(x => x.ProductId).ToArray();
        var products = await db.Products.Where(x => ids.Contains(x.Id) && x.IsActive).ToDictionaryAsync(x => x.Id, ct);
        if (products.Count != ids.Length) throw new RequestValidationException("Produto inexistente ou inativo.");
        var previous = quote.Items.ToDictionary(x => x.ProductId);
        db.QuoteItems.RemoveRange(quote.Items);
        quote.Items.Clear();
        foreach (var item in request.Items)
        {
            var product = products[item.ProductId]; previous.TryGetValue(item.ProductId, out var snapshot);
            var detail = new QuoteItem { Id = Guid.NewGuid(), ProductId = product.Id, ProductName = snapshot?.ProductName ?? product.Name, Sku = snapshot?.Sku ?? product.Sku, UnitPrice = snapshot?.UnitPrice ?? product.SalePrice, Quantity = item.Quantity, Discount = item.Discount };
            db.QuoteItems.Add(detail); quote.Items.Add(detail);
        }
        quote.CustomerId = customer.Id; quote.Customer = customer; quote.ValidUntil = request.ValidUntil; quote.Discount = request.Discount;
        quote.Notes = request.Notes; quote.UpdatedAt = now; quote.UpdatedByUserId = user; quote.Calculate();
        Audit(id.HasValue ? AuditAction.QuoteUpdated : AuditAction.QuoteCreated, quote, user, audit, now, new { quote.Number, quote.Total, ItemCount = quote.Items.Count });
        await db.SaveChangesAsync(ct); return Map(await Query().SingleAsync(x => x.Id == quote.Id, ct), DateOnly.FromDateTime(now.UtcDateTime));
    }, ct);
    public Task<QuoteView> TransitionAsync(Guid id, QuoteStatus next, byte[] version, string? reason, Guid user, SalesAuditContext audit, DateTimeOffset now, CancellationToken ct) => Write(async () =>
    {
        var quote = await Query().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new ResourceNotFoundException("Orçamento não encontrado.");
        db.Entry(quote).Property(x => x.RowVersion).OriginalValue = version;
        quote.Transition(next, DateOnly.FromDateTime(now.UtcDateTime)); quote.UpdatedAt = now; quote.UpdatedByUserId = user;
        var action = next switch { QuoteStatus.Sent => AuditAction.QuoteSent, QuoteStatus.Approved => AuditAction.QuoteApproved, QuoteStatus.Rejected => AuditAction.QuoteRejected, _ => AuditAction.QuoteCancelled };
        Audit(action, quote, user, audit, now, new { Status = next, Reason = reason });
        await db.SaveChangesAsync(ct); return Map(quote, DateOnly.FromDateTime(now.UtcDateTime));
    }, ct);
    public Task<SaleView> ConvertAsync(Guid id, ConvertQuoteRequest request, PaymentMethod method, byte[] version, bool allowNegativeStock, Guid user, SalesAuditContext audit, DateTimeOffset now, CancellationToken ct) => Write(async () =>
    {
        var quote = await Query().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new ResourceNotFoundException("Orçamento não encontrado.");
        quote.EnsureConvertible(DateOnly.FromDateTime(now.UtcDateTime));
        if (!quote.Customer.IsActive) throw new RequestValidationException("Cliente inativo.");
        db.Entry(quote).Property(x => x.RowVersion).OriginalValue = version;
        var items = quote.Items.Select(x => new CreateSaleItemRequest(x.ProductId, x.Quantity)).ToArray();
        var sale = await new SalesStore(db).CreateInTransactionAsync("quote:" + Guid.NewGuid().ToString("N"), user, method, quote.Discount + quote.Items.Sum(x => x.Discount), quote.CustomerId, items, allowNegativeStock, audit, now, request.FinancialTerms, ct, quote);
        quote.ConvertToSale(sale.Id, now); quote.UpdatedAt = now; quote.UpdatedByUserId = user;
        Audit(AuditAction.QuoteConverted, quote, user, audit, now, new { sale.Id, sale.Number, sale.Total });
        await db.SaveChangesAsync(ct); return sale;
    }, ct);
    public async Task<QuoteSummary> SummaryAsync(DateOnly today, CancellationToken ct)
    {
        var first = new DateOnly(today.Year, today.Month, 1);
        var quotes = await db.Quotes.AsNoTracking().Where(x => x.IssueDate >= first && x.IssueDate <= today).ToListAsync(ct);
        var statuses = quotes.Select(x => x.StatusOn(today)).ToArray();
        var converted = statuses.Count(x => x == QuoteStatus.Converted);
        var final = quotes.Count(x => x.Status != QuoteStatus.Draft && x.StatusOn(today) is QuoteStatus.Converted or QuoteStatus.Rejected or QuoteStatus.Expired or QuoteStatus.Cancelled);
        return new(quotes.Count, quotes.Sum(x => x.Total), statuses.Count(x => x == QuoteStatus.Approved),
            statuses.Count(x => x == QuoteStatus.Sent), converted, statuses.Count(x => x == QuoteStatus.Expired), final == 0 ? 0 : decimal.Round(100m * converted / final, 2));
    }
    private async Task<T> Write<T>(Func<Task<T>> action, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try { var result = await action(); await transaction.CommitAsync(ct); return result; }
        catch (DbUpdateConcurrencyException) { throw new ResourceConflictException("Orçamento, estoque ou caixa alterado. Atualize e tente novamente."); }
        catch (DbUpdateException) { throw new ResourceConflictException("Não foi possível gravar. Verifique duplicidades e referências."); }
        catch (SqlException e) when (e.Number == 1205) { throw new ResourceConflictException("Operação concorrente. Atualize e tente novamente."); }
        catch (InvalidOperationException e) { throw new ResourceConflictException(e.Message); }
        catch (ArgumentException e) { throw new RequestValidationException(e.Message); }
    }
    private void Audit(AuditAction action, Quote quote, Guid user, SalesAuditContext context, DateTimeOffset now, object data) =>
        db.AuditLogs.Add(new AuditLog { UserId = user, Action = action, EntityName = nameof(Quote), EntityId = quote.Id.ToString(), AfterData = JsonSerializer.Serialize(data), CorrelationId = context.CorrelationId, IpAddress = context.IpAddress, OccurredAt = now });
    private static QuoteView Map(Quote q, DateOnly today) => new(q.Id, q.Number, q.CustomerId, q.Customer.Name, q.IssueDate, q.ValidUntil, q.StatusOn(today).ToString(), q.Notes, q.Subtotal, q.Discount, q.Total, q.UserId, q.User.Name, q.SaleId, q.ConvertedAt, Convert.ToBase64String(q.RowVersion), q.Items.OrderBy(x => x.ProductName).Select(x => new QuoteItemView(x.Id, x.ProductId, x.ProductName, x.Sku, x.Quantity, x.UnitPrice, x.Discount, x.Total)).ToArray(), q.Status == QuoteStatus.Draft);
    public ValueTask DisposeAsync() => db.DisposeAsync();
}
