namespace Wrapster.Mailing.Templates;

public interface ITemplateRenderer
{
    Task<RenderedTemplate?> RenderAsync(
        string templateName,
        IReadOnlyDictionary<string, object?>? tokens,
        CancellationToken cancellationToken = default);
}
