using System.Globalization;
using System.Text;
using Forjix.Application.Common;
using Forjix.Application.Features.Settings;
namespace Forjix.Application.Features.Quotes;
internal static class QuotePdf
{
    private static readonly CultureInfo Brazilian = CultureInfo.GetCultureInfo("pt-BR");
    public static byte[] Create(QuoteView quote, TenantSettingsView company, byte[]? image)
    {
        var logo = PdfDocument.ReadLogo(image);
        var pages = new List<string>(); var page = new StringBuilder(); var y = 800; var pageNumber = 0;
        void Line(string text, int size = 10)
        {
            if (y < 70) EndPage();
            page.Append(CultureInfo.InvariantCulture, $"BT /F1 {size} Tf 40 {y} Td ({PdfDocument.Text(text)}) Tj ET\n"); y -= size + 7;
        }
        void Header()
        {
            pageNumber++; y = 800;
            if (logo is not null)
            {
                var h = 40m; var w = Math.Min(120m, h * logo.Width / logo.Height); h = w * logo.Height / logo.Width;
                page.Append(CultureInfo.InvariantCulture, $"q {w:0.##} 0 0 {h:0.##} 435 780 cm /Logo Do Q\n");
            }
            foreach (var text in Wrap(company.TradeName, 40)) Line(text, 16);
            Line(quote.Number + " - Orçamento comercial", 12);
            foreach (var text in Wrap((company.LegalName ?? "") + " | Documento: " + (company.Cnpj ?? "Não informado"), 88)) Line(text);
            foreach (var text in Wrap((company.Phone ?? "") + " | " + (company.Email ?? "") + " | " + (company.Address ?? ""), 88)) Line(text, 9);
            foreach (var text in Wrap("Cliente: " + quote.CustomerName, 70)) Line(text, 11);
            Line($"Emissão: {quote.IssueDate:dd/MM/yyyy}  |  Validade: {quote.ValidUntil:dd/MM/yyyy}  |  Status: {quote.Status}");
            foreach (var text in Wrap("Responsável: " + quote.UserName, 88)) Line(text);
            y -= 10;
            Line("PRODUTO / SKU", 10);
            foreach (var (text, x) in new[] { ("Quantidade", 40), ("Unitário", 155), ("Desconto", 285), ("Total", 410) })
                page.Append(CultureInfo.InvariantCulture, $"BT /F1 10 Tf {x} {y} Td ({text}) Tj ET\n");
            y -= 17;
        }
        void EndPage()
        {
            page.Append(CultureInfo.InvariantCulture, $"BT /F1 8 Tf 40 35 Td ({PdfDocument.Text(quote.Number)} - Página {pageNumber}) Tj ET\n");
            pages.Add(page.ToString()); page.Clear(); Header();
        }
        Header();
        foreach (var item in quote.Items)
        {
            var textLines = Wrap(item.ProductName + " | " + item.Sku, 85).ToArray();
            if (y < 70 + (textLines.Length + 2) * 17) EndPage();
            foreach (var text in textLines) Line(text);
            foreach (var (text, x) in new[] { (item.Quantity.ToString("N3", Brazilian), 40), (Money(item.UnitPrice), 155), (Money(item.Discount), 285), (Money(item.Total), 410) })
                page.Append(CultureInfo.InvariantCulture, $"BT /F1 9 Tf {x} {y} Td ({PdfDocument.Text(text)}) Tj ET\n");
            y -= 17;
            y -= 5;
        }
        if (y < 190) EndPage();
        y -= 10; Line("Subtotal: " + Money(quote.Subtotal), 12);
        Line("Descontos nos itens: " + Money(quote.Items.Sum(x => x.Discount)));
        Line("Desconto geral: " + Money(quote.Discount));
        Line("TOTAL: " + Money(quote.Total), 14);
        Line("Observações:", 10);
        foreach (var text in Wrap(quote.Notes ?? "Sem observações.", 88)) Line(text);
        Line("Este orçamento não reserva estoque. Atendimento sujeito à validade e aprovação.", 9);
        page.Append(CultureInfo.InvariantCulture, $"BT /F1 8 Tf 40 35 Td ({PdfDocument.Text(quote.Number)} - Página {pageNumber}) Tj ET\n");
        pages.Add(page.ToString());
        return PdfDocument.Create(pages, logo);
    }
    private static string Money(decimal value) => value.ToString("C2", Brazilian);
    private static IEnumerable<string> Wrap(string value, int width)
    {
        foreach (var paragraph in value.Replace("\r", "", StringComparison.Ordinal).Split('\n'))
        {
            var remaining = paragraph;
            while (remaining.Length > width)
            {
                var position = remaining.LastIndexOf(' ', width); if (position < 1) position = width;
                yield return remaining[..position]; remaining = remaining[position..].TrimStart();
            }
            yield return remaining;
        }
    }
}
