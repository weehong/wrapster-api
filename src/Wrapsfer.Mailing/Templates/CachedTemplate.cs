using Fluid;

namespace Wrapsfer.Mailing.Templates;

internal sealed record CachedTemplate(IFluidTemplate Subject, IFluidTemplate? Html, IFluidTemplate? Text);
