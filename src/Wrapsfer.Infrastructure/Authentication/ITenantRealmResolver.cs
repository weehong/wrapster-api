using Microsoft.AspNetCore.Http;

namespace Wrapsfer.Infrastructure.Authentication;

public interface ITenantRealmResolver
{
    string ResolveRealm(HttpRequest request);
}
