using System.Text;
using Wrapster.Application.Abstractions.Email;

namespace Wrapster.Application.Email.Templates;

public sealed class StockRecoveryTemplate : IEmailTemplate
{
    private readonly string _barcode;
    private readonly int _currentStock;
    private readonly string _productName;
    private readonly int _threshold;

    public StockRecoveryTemplate(string productName, string barcode, int currentStock, int threshold)
    {
        _productName = productName;
        _barcode = barcode;
        _currentStock = currentStock;
        _threshold = threshold;
    }

    public string Subject => $"Stock Recovered: {_productName} ({_currentStock} in stock)";

    public string RenderHtml()
    {
        StringBuilder html = new();
        html.Append("<div style=\"font-family: sans-serif; max-width: 600px; margin: 0 auto;\">");
        html.Append("<h2 style=\"color: #2e7d32;\">Stock Recovered</h2>");
        html.Append(
            $"<p>The following product is back at or above the stock threshold of <strong>{_threshold}</strong>:</p>");
        html.Append("<table style=\"border-collapse: collapse; width: 100%; margin: 16px 0;\">");
        html.Append(
            "<tr style=\"background: #f5f5f5;\"><td style=\"padding: 8px; border: 1px solid #e0e0e0;\"><strong>Product</strong></td>");
        html.Append($"<td style=\"padding: 8px; border: 1px solid #e0e0e0;\">{_productName}</td></tr>");
        html.Append("<tr><td style=\"padding: 8px; border: 1px solid #e0e0e0;\"><strong>Barcode</strong></td>");
        html.Append($"<td style=\"padding: 8px; border: 1px solid #e0e0e0;\">{_barcode}</td></tr>");
        html.Append(
            "<tr style=\"background: #f5f5f5;\"><td style=\"padding: 8px; border: 1px solid #e0e0e0;\"><strong>Current Stock</strong></td>");
        html.Append(
            $"<td style=\"padding: 8px; border: 1px solid #e0e0e0; color: #2e7d32; font-weight: bold;\">{_currentStock}</td></tr>");
        html.Append("</table>");
        html.Append("<hr style=\"border: none; border-top: 1px solid #e0e0e0; margin: 24px 0;\" />");
        html.Append("<p style=\"color: #888; font-size: 12px;\">This is an automated email from Wrapster.</p>");
        html.Append("</div>");
        return html.ToString();
    }
}
