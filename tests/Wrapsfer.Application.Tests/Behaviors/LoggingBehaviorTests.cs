using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Wrapsfer.Application.Behaviors;
using Wrapsfer.Domain.Common;

namespace Wrapsfer.Application.Tests.Behaviors;

public class LoggingBehaviorTests
{
    [Fact]
    public async Task Handle_LogsRequestAndCallsNext()
    {
        LoggingBehavior<TestRequest, Result> behavior =
            new(NullLogger<LoggingBehavior<TestRequest, Result>>.Instance);
        TestRequest request = new("test-value");

        Result result = await behavior.Handle(request, _ => Task.FromResult(Result.Success()),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    public sealed record TestRequest(string Value) : IRequest<Result>;
}
