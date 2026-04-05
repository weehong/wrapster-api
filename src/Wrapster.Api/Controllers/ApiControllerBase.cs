using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wrapster.Domain.Common;

namespace Wrapster.Api.Controllers;

[ApiController]
[Authorize]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public abstract class ApiControllerBase : ControllerBase
{
    protected IActionResult ToActionResult(Result result)
    {
        if (result.IsSuccess)
        {
            return NoContent();
        }

        return ToErrorResult(result.Error);
    }

    protected IActionResult ToActionResult<T>(Result<T> result)
    {
        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        return ToErrorResult(result.Error);
    }

    protected IActionResult ToCreatedResult<T>(Result<T> result, string? routeName = null, object? routeValues = null)
    {
        if (result.IsSuccess)
        {
            if (routeName is not null)
            {
                return CreatedAtRoute(routeName, routeValues, result.Value);
            }

            return StatusCode(StatusCodes.Status201Created, result.Value);
        }

        return ToErrorResult(result.Error);
    }

    private IActionResult ToErrorResult(Error error) =>
        error.Type switch
        {
            ErrorType.NotFound => NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = error.Code,
                Detail = error.Description
            }),
            ErrorType.Validation => BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = error.Code,
                Detail = error.Description
            }),
            ErrorType.Conflict => Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = error.Code,
                Detail = error.Description
            }),
            _ => BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = error.Code,
                Detail = error.Description
            })
        };
}
