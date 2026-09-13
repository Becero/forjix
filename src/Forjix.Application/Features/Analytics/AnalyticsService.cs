using System.Globalization;
using System.Text;
using System.Xml;
using Forjix.Application.Abstractions.Analytics;
using Forjix.Application.Abstractions.Identity;
using Forjix.Application.Abstractions.Tenancy;
using Forjix.Application.Common;

namespace Forjix.Application.Features.Analytics;

internal sealed class AnalyticsService(
    ITenantDatabaseResolver resolver,
    IAnalyticsStoreFactory factory,
    ICurrentUser current,
    TimeProvider clock) : IAnalyticsService
{
    public async Task<DashboardView> DashboardAsync(CancellationToken ct = default)
    {
        var store = await Store(ct);
        await using (store) return await store.DashboardAsync(clock.GetUtcNow(), ct);
    }

    public async Task<ReportView> ReportAsync(DateTimeOffset? from, DateTimeOffset? through, CancellationToken ct = default)
    {
        var end = InclusiveEnd(through) ?? clock.GetUtcNow();
        var start = from ?? end.AddDays(-30);
        if (start > end) throw new RequestValidationException("Período inválido.");
        var store = await Store(ct);
        await using (store) return await store.ReportAsync(start, end, ct);
    }

    public async Task<ExportedReport> ExportAsync(DateTimeOffset? from, DateTimeOffset? through, string format, CancellationToken ct = default)
    {
        var report = await ReportAsync(from, through, ct);
        return format.ToLowerInvariant() switch
        {
            "excel" or "xls" => new(Excel(report), "application/vnd.ms-excel", $"forjix-relatorio-{DateTime.UtcNow:yyyyMMdd}.xls"),
            "pdf" => new(Pdf(report), "application/pdf", $"forjix-relatorio-{DateTime.UtcNow:yyyyMMdd}.pdf"),
            _ => throw new RequestValidationException("Formato de exportação inválido.")
        };
    }

    private async Task<IAnalyticsStore> Store(CancellationToken ct)
    {
        if (current.TenantId is not { } id) throw new ResourceNotFoundException("Tenant não encontrado.");
        return factory.Create(await resolver.ResolveByTenantIdAsync(id, ct) ?? throw new ResourceNotFoundException("Tenant não encontrado."));
    }

    private static byte[] Excel(ReportView report)
    {
        var output = new StringBuilder();
        using (var writer = XmlWriter.Create(output, new XmlWriterSettings { Encoding = Encoding.UTF8, Indent = true }))
        {
            writer.WriteStartElement("Workbook", "urn:schemas-microsoft-com:office:spreadsheet");
            writer.WriteAttributeString("xmlns", "ss", null, "urn:schemas-microsoft-com:office:spreadsheet");
            Sheet(writer, "Resumo", [
                ["Indicador", "Valor"], ["Faturamento", Number(report.Revenue)], ["Vendas", report.SaleCount.ToString(CultureInfo.InvariantCulture)],
                ["Ticket médio", Number(report.AverageTicket)], ["Compras recebidas", Number(report.PurchasesTotal)], ["Movimentações", report.StockMovementCount.ToString(CultureInfo.InvariantCulture)]]);
            Sheet(writer, "Vendas por pagamento", [["Forma", "Vendas", "Total"], .. report.Payments.Select(x => new[] { x.PaymentMethod, x.Count.ToString(CultureInfo.InvariantCulture), Number(x.Total) })]);
            Sheet(writer, "Produtos vendidos", [["Produto", "Quantidade", "Receita"], .. report.TopProducts.Select(x => new[] { x.ProductName, Number(x.Quantity), Number(x.Revenue) })]);
            Sheet(writer, "Compras", [["Número", "Fornecedor", "Data", "Status", "Total"], .. report.Purchases.Select(x => new[] { x.Number, x.Supplier, x.CreatedAt.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture), x.Status, Number(x.Total) })]);
            Sheet(writer, "Movimentações", [["Data", "Produto", "SKU", "Tipo", "Quantidade", "Saldo anterior", "Novo saldo", "Responsável"], .. report.StockMovements.Select(x => new[] { x.CreatedAt.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture), x.Product, x.Sku, x.Type, Number(x.Quantity), Number(x.PreviousQuantity), Number(x.NewQuantity), x.UserName })]);
            Sheet(writer, "Estoque", [["Produto", "SKU", "Saldo", "Mínimo", "Custo", "Status"], .. report.Inventory.Select(x => new[] { x.Product, x.Sku, Number(x.Quantity), Number(x.MinimumStock), Number(x.CostValue), x.Status })]);
            writer.WriteEndElement();
        }
        return Encoding.UTF8.GetBytes(output.ToString());
    }

    private static void Sheet(XmlWriter writer, string name, IEnumerable<string[]> rows)
    {
        writer.WriteStartElement("Worksheet");
        writer.WriteAttributeString("ss", "Name", null, name);
        writer.WriteStartElement("Table");
        foreach (var row in rows)
        {
            writer.WriteStartElement("Row");
            foreach (var value in row) Cell(writer, value);
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static void Cell(XmlWriter writer, string value)
    {
        writer.WriteStartElement("Cell");
        writer.WriteStartElement("Data");
        writer.WriteAttributeString("ss", "Type", null, "String");
        writer.WriteString(value);
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static string Number(decimal value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static DateTimeOffset? InclusiveEnd(DateTimeOffset? value) =>
        value.HasValue && value.Value.TimeOfDay == TimeSpan.Zero ? value.Value.AddDays(1).AddTicks(-1) : value;

    private static byte[] Pdf(ReportView report)
    {
        var lines = new List<string>
        {
            "FORJIX - RELATORIO GERENCIAL",
            $"Periodo: {report.From:dd/MM/yyyy} a {report.Through:dd/MM/yyyy}",
            $"Faturamento: R$ {report.Revenue:F2} | Vendas: {report.SaleCount} | Ticket medio: R$ {report.AverageTicket:F2}",
            $"Compras recebidas: R$ {report.PurchasesTotal:F2} | Movimentacoes: {report.StockMovementCount}",
            "", "PRODUTOS MAIS VENDIDOS"
        };
        lines.AddRange(report.TopProducts.Take(8).Select(x => $"{x.ProductName} - {x.Quantity:N3} un. - R$ {x.Revenue:F2}"));
        lines.AddRange(["", "COMPRAS RECENTES"]);
        lines.AddRange(report.Purchases.Take(8).Select(x => $"{x.Number} - {x.Supplier} - {x.Status} - R$ {x.Total:F2}"));
        lines.AddRange(["", "ESTOQUE BAIXO"]);
        lines.AddRange(report.LowStock.Take(8).Select(x => $"{x.ProductName} - saldo {x.Quantity:N3} / minimo {x.MinimumStock:N3}"));
        var content = "BT /F1 10 Tf 42 800 Td " + string.Join(" Tj 0 -16 Td ", lines.Take(45).Select(x => $"({Escape(Ascii(x))})")) + " Tj ET";
        var objects = new[] { "<< /Type /Catalog /Pages 2 0 R >>", "<< /Type /Pages /Kids [3 0 R] /Count 1 >>", "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>", $"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}\nendstream", "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>" };
        var pdf = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int> { 0 };
        for (var index = 0; index < objects.Length; index++)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(pdf.ToString()));
            pdf.Append(CultureInfo.InvariantCulture, $"{index + 1} 0 obj\n{objects[index]}\nendobj\n");
        }
        var xref = Encoding.ASCII.GetByteCount(pdf.ToString());
        pdf.Append(CultureInfo.InvariantCulture, $"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1)) pdf.Append(CultureInfo.InvariantCulture, $"{offset:0000000000} 00000 n \n");
        pdf.Append(CultureInfo.InvariantCulture, $"trailer << /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF");
        return Encoding.ASCII.GetBytes(pdf.ToString());
    }

    private static string Escape(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("(", "\\(", StringComparison.Ordinal).Replace(")", "\\)", StringComparison.Ordinal);

    private static string Ascii(string value) => new(value.Normalize(NormalizationForm.FormD)
        .Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark && character <= 127).ToArray());
}
