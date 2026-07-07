namespace Wrapsfer.Mailing.Abstractions;

public sealed record MailBody
{
    public MailBody(string? html = null, string? text = null)
    {
        if (string.IsNullOrWhiteSpace(html) && string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException(
                "MailBody requires at least one of Html or Text to be non-empty.");
        }

        Html = string.IsNullOrWhiteSpace(html) ? null : html;
        Text = string.IsNullOrWhiteSpace(text) ? null : text;
    }

    public string? Html { get; }
    public string? Text { get; }

    public static MailBody FromHtml(string html) => new(html);
    public static MailBody FromText(string text) => new(text: text);
    public static MailBody FromBoth(string html, string text) => new(html, text);
}
