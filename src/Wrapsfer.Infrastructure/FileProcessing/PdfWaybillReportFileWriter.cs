using System.Globalization;
using System.Text;
using Wrapsfer.Application.Abstractions.FileProcessing;
using Wrapsfer.Application.Waybills.Commands.RequestWaybillsExport;

namespace Wrapsfer.Infrastructure.FileProcessing;

internal sealed class PdfWaybillReportFileWriter : IWaybillReportFileWriter
{
    private const int RowsPerPage = 34;

    public Task<byte[]> WriteAsync(IReadOnlyList<WaybillReportRow> rows, WaybillExportFormat format,
        CancellationToken cancellationToken = default)
    {
        List<string> pages = BuildPages(rows);
        byte[] bytes = BuildPdf(pages);
        return Task.FromResult(bytes);
    }

    private static List<string> BuildPages(IReadOnlyList<WaybillReportRow> rows)
    {
        List<string> rowLines = rows.Count == 0
            ? ["No waybills matched the selected filters."]
            : rows.Select(ToLine).ToList();

        List<string> pages = [];
        for (int index = 0; index < rowLines.Count; index += RowsPerPage)
        {
            IEnumerable<string> pageRows = rowLines.Skip(index).Take(RowsPerPage);
            StringBuilder content = new();
            content.AppendLine("BT");
            content.AppendLine("/F1 14 Tf");
            content.AppendLine("40 790 Td");
            content.AppendLine($"({Escape("Waybill Report")}) Tj");
            content.AppendLine("/F1 8 Tf");
            content.AppendLine("12 TL");
            content.AppendLine("0 -20 Td");
            content.AppendLine($"({Escape("Tenant | Date | Waybill | Status | Product | Qty")}) Tj");
            content.AppendLine("T*");

            foreach (string line in pageRows)
            {
                content.AppendLine($"({Escape(line)}) Tj");
                content.AppendLine("T*");
            }

            content.AppendLine("ET");
            pages.Add(content.ToString());
        }

        return pages;
    }

    private static string ToLine(WaybillReportRow row)
    {
        string product = string.IsNullOrWhiteSpace(row.ProductName)
            ? row.ProductBarcode ?? string.Empty
            : $"{row.ProductName} ({row.ProductBarcode})";

        return string.Join(" | ",
            row.TenantId,
            row.PackagingDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            row.WaybillNumber,
            row.Status.ToString(),
            Truncate(product, 42),
            row.Quantity?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
    }

    private static byte[] BuildPdf(IReadOnlyList<string> pageContents)
    {
        List<string> objects = [];
        List<int> pageObjectIds = [];
        const int catalogObjectId = 1;
        const int pagesObjectId = 2;
        const int fontObjectId = 3;

        objects.Add($"{catalogObjectId} 0 obj\n<< /Type /Catalog /Pages {pagesObjectId} 0 R >>\nendobj\n");
        objects.Add(string.Empty);
        objects.Add($"{fontObjectId} 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>\nendobj\n");

        int nextObjectId = 4;
        foreach (string content in pageContents)
        {
            int pageObjectId = nextObjectId++;
            int contentObjectId = nextObjectId++;
            pageObjectIds.Add(pageObjectId);

            objects.Add(
                $"{pageObjectId} 0 obj\n<< /Type /Page /Parent {pagesObjectId} 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 {fontObjectId} 0 R >> >> /Contents {contentObjectId} 0 R >>\nendobj\n");
            objects.Add(
                $"{contentObjectId} 0 obj\n<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}endstream\nendobj\n");
        }

        string kids = string.Join(" ", pageObjectIds.Select(id => $"{id} 0 R"));
        objects[pagesObjectId - 1] =
            $"{pagesObjectId} 0 obj\n<< /Type /Pages /Kids [{kids}] /Count {pageObjectIds.Count} >>\nendobj\n";

        StringBuilder pdf = new("%PDF-1.4\n");
        List<int> offsets = [0];
        foreach (string obj in objects)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(pdf.ToString()));
            pdf.Append(obj);
        }

        int xrefOffset = Encoding.ASCII.GetByteCount(pdf.ToString());
        pdf.AppendLine("xref");
        pdf.AppendLine($"0 {objects.Count + 1}");
        pdf.AppendLine("0000000000 65535 f ");
        foreach (int offset in offsets.Skip(1))
        {
            pdf.AppendLine($"{offset.ToString("0000000000", CultureInfo.InvariantCulture)} 00000 n ");
        }

        pdf.AppendLine("trailer");
        pdf.AppendLine($"<< /Size {objects.Count + 1} /Root {catalogObjectId} 0 R >>");
        pdf.AppendLine("startxref");
        pdf.AppendLine(xrefOffset.ToString(CultureInfo.InvariantCulture));
        pdf.AppendLine("%%EOF");

        return Encoding.ASCII.GetBytes(pdf.ToString());
    }

    private static string Escape(string value) => value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("(", "\\(", StringComparison.Ordinal)
        .Replace(")", "\\)", StringComparison.Ordinal);

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
