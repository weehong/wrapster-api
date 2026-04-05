using System.Text;
using Wrapster.Application.Abstractions.Email;

namespace Wrapster.Application.Email.Templates;

public sealed class ReportEmailTemplate : IEmailTemplate
{
    private readonly string _dateRange;
    private readonly EmailAttachment _fileAttachment;
    private readonly string _reportName;

    public ReportEmailTemplate(string reportName, string dateRange, byte[] fileContent, string fileContentType)
    {
        _reportName = reportName;
        _dateRange = dateRange;

        string extension = fileContentType switch
        {
            "application/pdf" => ".pdf",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" => ".xlsx",
            _ => ".bin"
        };

        _fileAttachment = new EmailAttachment(
            $"{reportName}{extension}",
            fileContent,
            fileContentType);
    }

    public string Subject => $"{_reportName} - {_dateRange}";

    public IReadOnlyList<EmailAttachment> Attachments => [_fileAttachment];

    public string RenderHtml()
    {
        StringBuilder html = new();
        html.Append("<div style=\"font-family: sans-serif; max-width: 600px; margin: 0 auto;\">");
        html.Append("<h2 style=\"color: #424242;\">Packaging Report</h2>");
        html.Append(
            $"<p>Please find attached the <strong>{_reportName}</strong> report for the period <strong>{_dateRange}</strong>.</p>");
        html.Append("<hr style=\"border: none; border-top: 1px solid #e0e0e0; margin: 24px 0;\" />");
        html.Append("<p style=\"color: #888; font-size: 12px;\">This is an automated email from Wrapster.</p>");
        html.Append("</div>");
        return html.ToString();
    }
}
