using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Wrapsfer.Application.Behaviors;

public sealed class LoggingBehavior<TRequest, TResponse>(
    ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        string requestName = typeof(TRequest).Name;
        string sanitizedRequest = RequestLoggingSanitizer.Sanitize(request);

        logger.LogInformation("{RequestName} {Request}", requestName, sanitizedRequest);

        Stopwatch stopWatch = Stopwatch.StartNew();
        TResponse response = await next(cancellationToken);

        stopWatch.Stop();

        logger.LogInformation("{RequestName} {Request} - {ElapsedMilliseconds} ms",
            requestName,
            sanitizedRequest,
            stopWatch.ElapsedMilliseconds);

        return response;
    }
}
