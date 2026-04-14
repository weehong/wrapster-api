using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wrapsfer.Application.Abstractions;

namespace Wrapsfer.Api.Controllers.V1;

[ApiController]
[Authorize]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public sealed class MeController : ControllerBase
{
    [HttpGet]
    public IActionResult Get([FromServices] ITenantContext tenantContext) => Ok(new
    {
        tenantContext.UserId,
        tenantContext.TenantId,
        tenantContext.Roles
    });
}
