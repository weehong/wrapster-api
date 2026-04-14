using Fluid;

namespace Wrapster.Mailing.Templates;

internal sealed record CachedTemplate(IFluidTemplate Subject, IFluidTemplate? Html, IFluidTemplate? Text);
