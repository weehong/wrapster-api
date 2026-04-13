using System.Net;
using System.Text;
using Wrapster.Application.Abstractions.Email;

namespace Wrapster.Application.Email.Templates;

public sealed class ProductsExportTemplate : IEmailTemplate
{
    private readonly EmailAttachment _fileAttachment;
    private readonly int _productCount;
    private readonly DateTime _requestedAt;

    public ProductsExportTemplate(byte[] fileContent, string fileContentType, int productCount, DateTime requestedAt)
    {
        _productCount = productCount;
        _requestedAt = requestedAt;

        string extension = fileContentType switch
        {
            "text/csv" => ".csv",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" => ".xlsx",
            _ => ".bin"
        };

        _fileAttachment = new EmailAttachment($"products-export-{requestedAt:yyyyMMdd-HHmmss}{extension}",
            fileContent, fileContentType);
    }

    public string Subject => $"Wrapster products export — {_requestedAt:yyyy-MM-dd}";

    public IReadOnlyList<EmailAttachment> Attachments => [_fileAttachment];

    public string RenderHtml()
    {
        StringBuilder html = new();
        html.Append("<div style=\"font-family: sans-serif; max-width: 600px; margin: 0 auto;\">");
        html.Append("<h2 style=\"color: #424242;\">Your products export is ready</h2>");
        html.Append(
            $"<p>Attached is your product catalog export containing <strong>{_productCount}</strong> products, requested on <strong>{WebUtility.HtmlEncode(_requestedAt.ToString("yyyy-MM-dd HH:mm"))} UTC</strong>.</p>");
        html.Append("<hr style=\"border: none; border-top: 1px solid #e0e0e0; margin: 24px 0;\" />");
        html.Append("<p style=\"color: #888; font-size: 12px;\">This is an automated email from Wrapster.</p>");
        html.Append("</div>");
        return html.ToString();
    }
}
