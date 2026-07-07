namespace Wrapsfer.Infrastructure.Authentication;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class AllowRestrictedTokenAttribute : Attribute
{
}
