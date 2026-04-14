namespace Wrapster.Mailing.Errors;

public static class MailingErrors
{
    public const string InvalidMessage = "Mailing.InvalidMessage";
    public const string MissingRecipient = "Mailing.MissingRecipient";
    public const string EmptyBody = "Mailing.EmptyBody";
    public const string TemplateNotFound = "Mailing.TemplateNotFound";
    public const string TransientSendFailure = "Mailing.TransientSendFailure";
    public const string PermanentSendFailure = "Mailing.PermanentSendFailure";
    public const string TemplateRenderFailure = "Mailing.TemplateRenderFailure";
}
