using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Encodings.Web;
using Fluid;

namespace Wrapsfer.Mailing.Templates;

internal sealed class FluidTemplateRenderer : ITemplateRenderer
{
    private const string ResourcePrefix = "Wrapsfer.Mailing.Templates.Files.";
    private static readonly FluidParser s_parser = new();
    private static readonly Assembly s_assembly = typeof(FluidTemplateRenderer).Assembly;

    private readonly ConcurrentDictionary<string, CachedTemplate?> _cache = new();

    public async Task<RenderedTemplate?> RenderAsync(
        string templateName,
        IReadOnlyDictionary<string, object?>? tokens,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(templateName))
        {
            return null;
        }

        CachedTemplate? cached = _cache.GetOrAdd(templateName, Load);
        if (cached is null)
        {
            return null;
        }

        TemplateOptions options = new();
        TemplateContext context = new(options);
        if (tokens is not null)
        {
            foreach ((string key, object? value) in tokens)
            {
                context.SetValue(key, value);
            }
        }

        string subject = await cached.Subject.RenderAsync(context, NullEncoder.Default);
        string? html = cached.Html is null ? null : await cached.Html.RenderAsync(context, HtmlEncoder.Default);
        string? text = cached.Text is null ? null : await cached.Text.RenderAsync(context, NullEncoder.Default);

        cancellationToken.ThrowIfCancellationRequested();

        return new RenderedTemplate(subject.Trim(), html, text);
    }

    private static CachedTemplate? Load(string templateName)
    {
        // MSBuild normalizes dashes in embedded-resource folder/file names to
        // underscores when generating manifest names; mirror that conversion here.
        string slug = templateName.Replace('-', '_');
        string? subjectSource = TryReadResource($"{ResourcePrefix}{slug}.subject.liquid");
        string? htmlSource = TryReadResource($"{ResourcePrefix}{slug}.body.html.liquid");
        string? textSource = TryReadResource($"{ResourcePrefix}{slug}.body.text.liquid");

        if (subjectSource is null && htmlSource is null && textSource is null)
        {
            return null;
        }

        if (subjectSource is null)
        {
            throw new InvalidOperationException(
                $"Template '{templateName}' is missing a subject.liquid file.");
        }

        if (htmlSource is null && textSource is null)
        {
            throw new InvalidOperationException(
                $"Template '{templateName}' has no body.html.liquid or body.text.liquid file.");
        }

        IFluidTemplate subject = Parse(subjectSource, $"{templateName}/subject");
        IFluidTemplate? html = htmlSource is null ? null : Parse(htmlSource, $"{templateName}/body.html");
        IFluidTemplate? text = textSource is null ? null : Parse(textSource, $"{templateName}/body.text");

        return new CachedTemplate(subject, html, text);
    }

    private static IFluidTemplate Parse(string source, string identifier)
    {
        if (!s_parser.TryParse(source, out IFluidTemplate template, out string error))
        {
            throw new InvalidOperationException(
                $"Failed to parse template '{identifier}': {error}");
        }

        return template;
    }

    private static string? TryReadResource(string resourceName)
    {
        using Stream? stream = s_assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            return null;
        }

        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }
}
