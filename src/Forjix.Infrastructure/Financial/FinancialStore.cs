using System.Data;
using System.Text.Json;
using Forjix.Application.Abstractions.Financial;
using Forjix.Application.Abstractions.Sales;
using Forjix.Application.Abstractions.Tenancy;
using Forjix.Application.Common;
using Forjix.Application.Features.Financial;
using Forjix.Domain.Entities.Audit;
using Forjix.Domain.Entities.Cash;
using Forjix.Domain.Entities.Financial;
using Forjix.Domain.Enums;
using Forjix.Infrastructure.Persistence.Tenant;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Forjix.Infrastructure.Financial;

internal sealed class FinancialStoreFactory(ITenantDbContextFactory contexts) : IFinancialStoreFactory
{
    public IFinancialStore Create(ResolvedTenantDatabase tenant) => new FinancialStore(contexts.Create(tenant));
}

internal sealed class FinancialStore(TenantDbContext db) : IFinancialStore
{
    public async Task<IReadOnlyList<FinancialCategoryView>> CategoriesAsync(FinancialCategoryType? type, CancellationToken ct) =>
        await db.FinancialCategories.AsNoTracking().Where(x => !type.HasValue || x.Type == type)
            .OrderBy(x => x.Name).Select(x => new FinancialCategoryView(x.Id, x.Name, x.Type.ToString(), x.IsActive)).ToListAsync(ct);

    public async Task<FinancialCategoryView> SaveCategoryAsync(Guid? id, SaveFinancialCategoryRequest request, FinancialCategoryType type, Guid user, SalesAuditContext audit, DateTimeOffset now, CancellationToken ct)
    {
        return await Write(async () =>
        {
            var category = id.HasValue ? await db.FinancialCategories.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new ResourceNotFoundException("Categoria não encontrada.")
                : new FinancialCategory { Id = Guid.NewGuid(), Name = request.Name, Type = type, CreatedAt = now };
            if (await db.FinancialCategories.AnyAsync(x => x.Id != category.Id && x.Name == request.Name && x.Type == type, ct))
                throw new ResourceConflictException("Categoria já cadastrada.");
            if (category.Type != type && (await db.AccountsReceivable.AnyAsync(x => x.FinancialCategoryId == category.Id, ct) || await db.AccountsPayable.AnyAsync(x => x.FinancialCategoryId == category.Id, ct)))
                throw new ResourceConflictException("Não altere o tipo de uma categoria já utilizada.");
            category.Name = request.Name; category.Type = type; category.IsActive = request.IsActive; category.UpdatedAt = now;
            if (!id.HasValue) db.FinancialCategories.Add(category);
            Audit(id.HasValue ? AuditAction.FinancialCategoryUpdated : AuditAction.FinancialCategoryCreated, category.Id, user, audit, now, new { category.Name, category.Type, category.IsActive });
            await db.SaveChangesAsync(ct);
            return new FinancialCategoryView(category.Id, category.Name, category.Type.ToString(), category.IsActive);
        }, ct);
    }

    public async Task<PagedFinancialAccounts> ListAsync(FinancialOperationType type, FinancialAccountFilter f, DateOnly today, CancellationToken ct)
    {
        var q = Query(type).AsNoTracking();
        if (!string.IsNullOrWhiteSpace(f.Search)) q = q.Where(x => x.Description.Contains(f.Search) || (x.Document != null && x.Document.Contains(f.Search)));
        if (f.From.HasValue) q = q.Where(x => x.DueDate >= f.From);
        if (f.Through.HasValue) q = q.Where(x => x.DueDate <= f.Through);
        if (f.CategoryId.HasValue) q = q.Where(x => x.FinancialCategoryId == f.CategoryId);
        if (f.PartyId.HasValue) q = type == FinancialOperationType.Receivable
            ? q.OfType<AccountReceivable>().Where(x => x.CustomerId == f.PartyId)
            : q.OfType<AccountPayable>().Where(x => x.SupplierId == f.PartyId);
        if (f.OverdueOnly || string.Equals(f.Status, "Overdue", StringComparison.OrdinalIgnoreCase))
            q = q.Where(x => x.Status != FinancialAccountStatus.Cancelled && x.OpenAmount > 0 && x.DueDate < today);
        else if (!string.IsNullOrWhiteSpace(f.Status))
        {
            var status = Enum.Parse<FinancialAccountStatus>(f.Status, true);
            q = q.Where(x => x.Status == status && (x.OpenAmount == 0 || x.DueDate >= today || x.Status == FinancialAccountStatus.Cancelled));
        }
        var total = await q.CountAsync(ct);
        var entities = await q.OrderBy(x => x.DueDate).ThenBy(x => x.Id).Skip((f.Page - 1) * f.PageSize).Take(f.PageSize).ToListAsync(ct);
        return new(await Maps(entities, today, ct), f.Page, f.PageSize, total);
    }

    public async Task<FinancialAccountView?> GetAsync(FinancialOperationType type, Guid id, DateOnly today, CancellationToken ct)
    {
        var entity = await Query(type).AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        return entity is null ? null : (await Maps([entity], today, ct))[0];
    }

    public async Task<IReadOnlyList<FinancialAccountView>> CreateAsync(FinancialOperationType type, SaveFinancialAccountRequest request, Guid user, SalesAuditContext audit, DateTimeOffset now, CancellationToken ct) =>
        await Write(async () =>
        {
            await References(type, request.PartyId, request.FinancialCategoryId, ct);
            var amounts = FinancialRules.Split(request.Amount, request.Installments);
            var group = Guid.NewGuid(); var accounts = new List<FinancialAccount>();
            for (var i = 0; i < amounts.Count; i++)
            {
                FinancialAccount account = type == FinancialOperationType.Receivable ? new AccountReceivable { Description = request.Description, CustomerId = request.PartyId } : new AccountPayable { Description = request.Description, SupplierId = request.PartyId };
                account.Id = Guid.NewGuid(); account.GroupId = group; account.FinancialCategoryId = request.FinancialCategoryId;
                account.Document = request.Document; account.IssueDate = request.IssueDate; account.DueDate = request.DueDate.AddMonths(i);
                account.InstallmentNumber = i + 1; account.TotalInstallments = amounts.Count; account.Notes = request.Notes;
                account.CreatedByUserId = account.UpdatedByUserId = user; account.CreatedAt = account.UpdatedAt = now; account.SetAmount(amounts[i]);
                db.Add(account); accounts.Add(account);
                Audit(AuditAction.FinancialAccountCreated, account.Id, user, audit, now, new { account.OriginalAmount, account.InstallmentNumber, Type = type });
            }
            await db.SaveChangesAsync(ct);
            return await Maps(accounts, DateOnly.FromDateTime(now.UtcDateTime), ct);
        }, ct);

    public async Task<FinancialAccountView> UpdateAsync(FinancialOperationType type, Guid id, SaveFinancialAccountRequest request, byte[] version, Guid user, SalesAuditContext audit, DateTimeOffset now, CancellationToken ct) =>
        await Mutate(type, id, user, audit, now, async account =>
        {
            if (Source(account).HasValue) throw new ResourceConflictException("Contas originadas de venda/compra devem preservar seus valores e vínculos.");
            if (request.Installments != account.TotalInstallments) throw new RequestValidationException("Não altere a quantidade de parcelas de um título existente.");
            if (await PaymentQuery(type, id).AnyAsync(x => x.ReversedAt == null, ct)) throw new ResourceConflictException("Estorne as baixas antes de editar.");
            await References(type, request.PartyId, request.FinancialCategoryId, ct);
            db.Entry(account).Property(x => x.RowVersion).OriginalValue = version;
            account.SetAmount(request.Amount);
            account.Description = request.Description; account.Document = request.Document; account.IssueDate = request.IssueDate; account.DueDate = request.DueDate; account.Notes = request.Notes; account.FinancialCategoryId = request.FinancialCategoryId;
            if (account is AccountReceivable r) r.CustomerId = request.PartyId; else ((AccountPayable)account).SupplierId = request.PartyId;
            Audit(AuditAction.FinancialAccountUpdated, id, user, audit, now, new { account.OriginalAmount, account.DueDate });
        }, ct);

    public async Task<FinancialAccountView> CancelAsync(FinancialOperationType type, Guid id, string reason, byte[] version, Guid user, SalesAuditContext audit, DateTimeOffset now, CancellationToken ct) =>
        await Mutate(type, id, user, audit, now, async account =>
        {
            if (account is AccountReceivable { SaleId: not null }) throw new ResourceConflictException("Cancele a venda de origem para cancelar seus títulos.");
            if (await PaymentQuery(type, id).AnyAsync(x => x.ReversedAt == null, ct)) throw new ResourceConflictException("Estorne as baixas antes de cancelar.");
            db.Entry(account).Property(x => x.RowVersion).OriginalValue = version;
            account.Cancel(); Audit(AuditAction.FinancialAccountCancelled, id, user, audit, now, new { Reason = reason });
        }, ct);

    public async Task<IReadOnlyList<FinancialPaymentView>> PaymentsAsync(FinancialOperationType type, Guid id, CancellationToken ct)
    {
        if (!await Query(type).AnyAsync(x => x.Id == id, ct)) throw new ResourceNotFoundException("Conta não encontrada.");
        return await PaymentQuery(type, id).AsNoTracking().OrderByDescending(x => x.CreatedAt).Select(x => new FinancialPaymentView(x.Id, x.Amount, x.PrincipalAmount, x.PaymentDate, x.PaymentMethod.ToString(), x.Discount, x.Interest, x.Penalty, x.Notes, x.CashMovementId, x.ReversedAt, x.ReversalReason, Convert.ToBase64String(x.RowVersion))).ToListAsync(ct);
    }

    public async Task<FinancialAccountView> PayAsync(FinancialOperationType type, Guid id, string key, CreateFinancialPaymentRequest request, PaymentMethod method, byte[] version, Guid user, SalesAuditContext audit, DateTimeOffset now, CancellationToken ct) =>
        await Mutate(type, id, user, audit, now, async account =>
        {
            var previous = await db.FinancialPayments.SingleOrDefaultAsync(x => x.IdempotencyKey == key, ct);
            if (previous is not null)
            {
                if (previous.Type != type || (previous.AccountReceivableId ?? previous.AccountPayableId) != id || previous.Amount != request.Amount || previous.Discount != request.Discount || previous.Interest != request.Interest || previous.Penalty != request.Penalty || previous.PaymentMethod != method || previous.PaymentDate != request.PaymentDate)
                    throw new ResourceConflictException("Idempotency-Key já utilizada por outra baixa.");
                return;
            }
            var principal = FinancialRules.Principal(request.Amount, request.Discount, request.Interest, request.Penalty);
            if (request.PaymentDate < account.IssueDate) throw new RequestValidationException("O pagamento não pode anteceder a emissão da conta.");
            db.Entry(account).Property(x => x.RowVersion).OriginalValue = version;
            account.Pay(principal, request.PaymentDate);
            var payment = new FinancialPayment { Id = Guid.NewGuid(), Type = type, AccountReceivableId = type == FinancialOperationType.Receivable ? id : null, AccountPayableId = type == FinancialOperationType.Payable ? id : null, IdempotencyKey = key, Amount = request.Amount, PrincipalAmount = principal, Discount = request.Discount, Interest = request.Interest, Penalty = request.Penalty, PaymentMethod = method, PaymentDate = request.PaymentDate, Notes = request.Notes, CreatedByUserId = user, CreatedAt = now };
            if (method == PaymentMethod.Cash) payment.CashMovement = await Cash(type, request.Amount, false, user, now, ct);
            db.FinancialPayments.Add(payment);
            Audit(AuditAction.FinancialPaymentCreated, payment.Id, user, audit, now, new { AccountId = id, payment.Amount, payment.PrincipalAmount });
        }, ct);

    public async Task<FinancialAccountView> ReverseAsync(FinancialOperationType type, Guid id, Guid paymentId, string reason, byte[] version, Guid user, SalesAuditContext audit, DateTimeOffset now, CancellationToken ct) =>
        await Mutate(type, id, user, audit, now, async account =>
        {
            var payment = await PaymentQuery(type, id).SingleOrDefaultAsync(x => x.Id == paymentId, ct) ?? throw new ResourceNotFoundException("Baixa não encontrada.");
            db.Entry(payment).Property(x => x.RowVersion).OriginalValue = version;
            payment.Reverse(user, reason, now);
            account.Reverse(payment.PrincipalAmount);
            if (payment.CashMovementId.HasValue) payment.ReversalCashMovement = await Cash(type, payment.Amount, true, user, now, ct);
            Audit(AuditAction.FinancialPaymentReversed, payment.Id, user, audit, now, new { AccountId = id, payment.Amount, Reason = reason });
        }, ct);

    public async Task<FinancialDashboard> DashboardAsync(DateOnly from, DateOnly through, DateOnly today, CancellationToken ct)
    {
        var open = db.Set<FinancialAccount>().AsNoTracking().Where(x => x.Status != FinancialAccountStatus.Cancelled && x.OpenAmount > 0);
        var receivable = await open.OfType<AccountReceivable>().SumAsync(x => (decimal?)x.OpenAmount, ct) ?? 0;
        var payable = await open.OfType<AccountPayable>().SumAsync(x => (decimal?)x.OpenAmount, ct) ?? 0;
        var overdue = open.Where(x => x.DueDate < today);
        var count = await overdue.CountAsync(ct); var overdueAmount = await overdue.SumAsync(x => (decimal?)x.OpenAmount, ct) ?? 0;
        var payments = db.FinancialPayments.AsNoTracking().Where(x => x.PaymentDate >= from && x.PaymentDate <= through);
        var received = await payments.Where(x => x.Type == FinancialOperationType.Receivable).SumAsync(x => (decimal?)x.Amount, ct) ?? 0;
        var paid = await payments.Where(x => x.Type == FinancialOperationType.Payable).SumAsync(x => (decimal?)x.Amount, ct) ?? 0;
        var start = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var end = new DateTimeOffset(through.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);
        var reversals = db.FinancialPayments.AsNoTracking().Where(x => x.ReversedAt >= start && x.ReversedAt <= end);
        received -= await reversals.Where(x => x.Type == FinancialOperationType.Receivable).SumAsync(x => (decimal?)x.Amount, ct) ?? 0;
        paid -= await reversals.Where(x => x.Type == FinancialOperationType.Payable).SumAsync(x => (decimal?)x.Amount, ct) ?? 0;
        var upcoming = await open.Include(x => x.FinancialCategory).Where(x => x.DueDate >= today).OrderBy(x => x.DueDate).ThenBy(x => x.Id).Take(10).ToListAsync(ct);
        return new(receivable, payable, count, overdueAmount, received, paid, received - paid, await Maps(upcoming, today, ct));
    }

    private IQueryable<FinancialAccount> Query(FinancialOperationType type) => type == FinancialOperationType.Receivable
        ? db.AccountsReceivable.Include(x => x.FinancialCategory) : db.AccountsPayable.Include(x => x.FinancialCategory);
    private IQueryable<FinancialPayment> PaymentQuery(FinancialOperationType type, Guid id) => type == FinancialOperationType.Receivable
        ? db.FinancialPayments.Where(x => x.AccountReceivableId == id) : db.FinancialPayments.Where(x => x.AccountPayableId == id);

    private async Task References(FinancialOperationType type, Guid? party, Guid category, CancellationToken ct)
    {
        var expected = type == FinancialOperationType.Receivable ? FinancialCategoryType.Income : FinancialCategoryType.Expense;
        if (!await db.FinancialCategories.AnyAsync(x => x.Id == category && x.IsActive && x.Type == expected, ct)) throw new RequestValidationException("Categoria inexistente, inativa ou de tipo incompatível.");
        if (party.HasValue)
        {
            var exists = type == FinancialOperationType.Receivable ? await db.Customers.AnyAsync(x => x.Id == party && x.IsActive, ct) : await db.Suppliers.AnyAsync(x => x.Id == party && x.IsActive, ct);
            if (!exists) throw new RequestValidationException("Cliente/fornecedor inexistente ou inativo.");
        }
    }

    private async Task<CashMovement> Cash(FinancialOperationType type, decimal amount, bool reversal, Guid user, DateTimeOffset now, CancellationToken ct)
    {
        var session = await db.CashSessions.SingleOrDefaultAsync(x => x.Status == CashSessionStatus.Open, ct) ?? throw new ResourceConflictException("Abra o caixa para registrar ou estornar uma baixa em dinheiro.");
        db.Entry(session).Property(x => x.OpeningAmount).IsModified = true;
        var income = type == FinancialOperationType.Receivable ^ reversal;
        var movement = new CashMovement { CashSessionId = session.Id, Type = income ? CashMovementType.Supply : CashMovementType.Withdrawal, Amount = amount, Reason = reversal ? "Estorno financeiro" : "Baixa financeira", UserId = user, CreatedAt = now };
        db.CashMovements.Add(movement); return movement;
    }

    private async Task<FinancialAccountView> Mutate(FinancialOperationType type, Guid id, Guid user, SalesAuditContext audit, DateTimeOffset now, Func<FinancialAccount, Task> action, CancellationToken ct) =>
        await Write(async () =>
        {
            var account = await Query(type).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new ResourceNotFoundException("Conta não encontrada.");
            await action(account); account.UpdatedAt = now; account.UpdatedByUserId = user;
            await db.SaveChangesAsync(ct);
            return (await Maps([account], DateOnly.FromDateTime(now.UtcDateTime), ct))[0];
        }, ct);

    private async Task<T> Write<T>(Func<Task<T>> action, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try { var result = await action(); await transaction.CommitAsync(ct); return result; }
        catch (DbUpdateConcurrencyException) { throw new ResourceConflictException("Os dados foram alterados por outro usuário. Atualize e tente novamente."); }
        catch (DbUpdateException) { throw new ResourceConflictException("Não foi possível gravar. Verifique referências e duplicidades."); }
        catch (SqlException e) when (e.Number == 1205) { throw new ResourceConflictException("Operação concorrente. Atualize os dados e tente novamente."); }
        catch (InvalidOperationException e) { throw new ResourceConflictException(e.Message); }
        catch (ArgumentException e) { throw new RequestValidationException(e.Message); }
    }

    private async Task<IReadOnlyList<FinancialAccountView>> Maps(IReadOnlyList<FinancialAccount> accounts, DateOnly today, CancellationToken ct)
    {
        var customerIds = accounts.OfType<AccountReceivable>().Select(x => x.CustomerId).ToArray();
        var supplierIds = accounts.OfType<AccountPayable>().Select(x => x.SupplierId).ToArray();
        var customers = await db.Customers.AsNoTracking().Where(x => customerIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var suppliers = await db.Suppliers.AsNoTracking().Where(x => supplierIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var categoryIds = accounts.Select(x => x.FinancialCategoryId).ToArray();
        var categories = await db.FinancialCategories.AsNoTracking().Where(x => categoryIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        return accounts.Select(x => new FinancialAccountView(x.Id, x.GroupId, Party(x), Party(x) is { } id ? (x is AccountReceivable ? customers : suppliers).GetValueOrDefault(id) : null, Source(x), x.FinancialCategoryId, categories.GetValueOrDefault(x.FinancialCategoryId, ""), x.Description, x.Document, x.OriginalAmount, x.OpenAmount, x.IssueDate, x.DueDate, x.PaymentDate, x.StatusOn(today).ToString(), x.InstallmentNumber, x.TotalInstallments, x.Notes, Convert.ToBase64String(x.RowVersion))).ToArray();
    }
    private static Guid? Party(FinancialAccount x) => x is AccountReceivable r ? r.CustomerId : ((AccountPayable)x).SupplierId;
    private static Guid? Source(FinancialAccount x) => x is AccountReceivable r ? r.SaleId : ((AccountPayable)x).PurchaseId;
    private void Audit(AuditAction action, Guid id, Guid user, SalesAuditContext context, DateTimeOffset now, object data) =>
        db.AuditLogs.Add(new AuditLog { UserId = user, Action = action, EntityName = "Financial", EntityId = id.ToString(), AfterData = JsonSerializer.Serialize(data), CorrelationId = context.CorrelationId, IpAddress = context.IpAddress, OccurredAt = now });
    public ValueTask DisposeAsync() => db.DisposeAsync();
}
