using Microsoft.Extensions.Options;

namespace Wrapsfer.Mailing;

/// <summary>
/// Adapter that exposes a singleton <see cref="IOptions{TOptions}"/> as an
/// <see cref="IOptionsSnapshot{TOptions}"/>. Required because Resend.ResendClient depends on
/// <see cref="IOptionsSnapshot{ResendClientOptions}"/> while AddResend wires its consumer chain
/// as a singleton; the default scoped <c>IOptionsSnapshot</c> can't be resolved from the root
/// provider, and in .NET 10+ the singleton <c>IOptions</c> implementation no longer also
/// satisfies <c>IOptionsSnapshot</c>, so an explicit adapter is required.
/// Named options are not supported — every <see cref="Get(string?)"/> call returns the same value.
/// </summary>
internal sealed class SingletonOptionsSnapshot<TOptions> : IOptionsSnapshot<TOptions>
    where TOptions : class
{
    private readonly IOptions<TOptions> _options;

    public SingletonOptionsSnapshot(IOptions<TOptions> options) => _options = options;

    public TOptions Value => _options.Value;

    public TOptions Get(string? name) => _options.Value;
}
