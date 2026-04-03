using Microsoft.AspNetCore.Http;

namespace Wrapster.Infrastructure.Authentication;

public interface ITenantRealmResolver
{
    string ResolveRealm(HttpRequest request);
}
