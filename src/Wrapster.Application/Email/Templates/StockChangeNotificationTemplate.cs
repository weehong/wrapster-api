using System.Text;
using Wrapster.Application.Abstractions.Email;

namespace Wrapster.Application.Email.Templates;

public sealed class StockChangeNotificationTemplate : IEmailTemplate
{
    private readonly string _barcode;
    private readonly string _changedBy;
    private readonly int _newStock;
    private readonly int _previousStock;
    private readonly string _productName;

    public StockChangeNotificationTemplate(
        string productName,
        string barcode,
        int previousStock,
        int newStock,
        string changedBy)
    {
        _productName = productName;
        _barcode = barcode;
        _previousStock = previousStock;
        _newStock = newStock;
        _changedBy = changedBy;
    }

    public string Subject => $"Stock Change: {_productName} ({_previousStock} -> {_newStock})";

    public string RenderHtml()
    {
        string changeColor = _newStock < _previousStock ? "#d32f2f" : "#2e7d32";
        string changeDirection = _newStock < _previousStock ? "decreased" : "increased";
        int difference = Math.Abs(_newStock - _previousStock);

        StringBuilder html = new();
        html.Append("<div style=\"font-family: sans-serif; max-width: 600px; margin: 0 auto;\">");
        html.Append("<h2 style=\"color: #424242;\">Stock Change Notification</h2>");
        html.Append(
            $"<p>Stock for <strong>{_productName}</strong> has {changeDirection} by <strong>{difference}</strong> unit(s).</p>");
        html.Append("<table style=\"border-collapse: collapse; width: 100%; margin: 16px 0;\">");
        html.Append(
            "<tr style=\"background: #f5f5f5;\"><td style=\"padding: 8px; border: 1px solid #e0e0e0;\"><strong>Product</strong></td>");
        html.Append($"<td style=\"padding: 8px; border: 1px solid #e0e0e0;\">{_productName}</td></tr>");
        html.Append("<tr><td style=\"padding: 8px; border: 1px solid #e0e0e0;\"><strong>Barcode</strong></td>");
        html.Append($"<td style=\"padding: 8px; border: 1px solid #e0e0e0;\">{_barcode}</td></tr>");
        html.Append(
            "<tr style=\"background: #f5f5f5;\"><td style=\"padding: 8px; border: 1px solid #e0e0e0;\"><strong>Previous Stock</strong></td>");
        html.Append($"<td style=\"padding: 8px; border: 1px solid #e0e0e0;\">{_previousStock}</td></tr>");
        html.Append("<tr><td style=\"padding: 8px; border: 1px solid #e0e0e0;\"><strong>New Stock</strong></td>");
        html.Append(
            $"<td style=\"padding: 8px; border: 1px solid #e0e0e0; color: {changeColor}; font-weight: bold;\">{_newStock}</td></tr>");
        html.Append(
            "<tr style=\"background: #f5f5f5;\"><td style=\"padding: 8px; border: 1px solid #e0e0e0;\"><strong>Changed By</strong></td>");
        html.Append($"<td style=\"padding: 8px; border: 1px solid #e0e0e0;\">{_changedBy}</td></tr>");
        html.Append("</table>");
        html.Append("<hr style=\"border: none; border-top: 1px solid #e0e0e0; margin: 24px 0;\" />");
        html.Append("<p style=\"color: #888; font-size: 12px;\">This is an automated email from Wrapster.</p>");
        html.Append("</div>");
        return html.ToString();
    }
}
