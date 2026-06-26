using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Abstractions.Auditing;
using Wrapsfer.Application.Behaviors;
using Wrapsfer.Domain.Common;

namespace Wrapsfer.Application.Tests.Behaviors;

public class AuditBehaviorTests
{
    private readonly AuditMetadataAccumulator _accumulator = new();
    private readonly Mock<IAuditSink> _sink = new();
    private readonly Mock<ITenantContext> _tenantContext = new();

    private AuditLogRecord? _written;

    public AuditBehaviorTests()
    {
        _tenantContext.Setup(t => t.TenantId).Returns("partner-a");
        _tenantContext.Setup(t => t.UserId).Returns("user-1");
        _tenantContext.Setup(t => t.Username).Returns("operator");
        _tenantContext.Setup(t => t.DisplayName).Returns("Ada Lovelace");
        _tenantContext.Setup(t => t.ActorRealm).Returns("owner-realm");

        _sink
            .Setup(s => s.WriteAsync(It.IsAny<AuditLogRecord>(), It.IsAny<CancellationToken>()))
            .Callback<AuditLogRecord, CancellationToken>((record, _) => _written = record)
            .Returns(Task.CompletedTask);
    }

    private AuditBehavior<TestRequest, Result> CreateBehavior() =>
        new(_accumulator, _sink.Object, _tenantContext.Object,
            NullLogger<AuditBehavior<TestRequest, Result>>.Instance);

    [Fact]
    public async Task Handle_WhenSuccess_WritesExecutedRecordWithTenantAndUser()
    {
        Result result = await CreateBehavior().Handle(
            new TestRequest("x"), _ => Task.FromResult(Result.Success()), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _written.Should().NotBeNull();
        _written!.EntityName.Should().Be(nameof(TestRequest));
        _written.Action.Should().Be(AuditAction.Executed);
        _written.TenantId.Should().Be("partner-a");
        _written.UserId.Should().Be("user-1");
        _written.Username.Should().Be("operator");
        _written.ActorName.Should().Be("Ada Lovelace");
        _written.ActorRealm.Should().Be("owner-realm");
        _written.EntityId.Should().NotBeNullOrWhiteSpace();

        JsonElement changes = JsonDocument.Parse(_written.Changes!).RootElement;
        changes.TryGetProperty("elapsedMilliseconds", out _).Should().BeTrue();
        changes.TryGetProperty("request", out _).Should().BeTrue();
        changes.TryGetProperty("errorCode", out _).Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenResultFailure_WritesFailedRecordWithErrorCode()
    {
        Error error = new("Test.Boom", "boom", ErrorType.Failure);

        Result result = await CreateBehavior().Handle(
            new TestRequest("x"), _ => Task.FromResult(Result.Failure(error)), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        _written!.Action.Should().Be(AuditAction.Failed);

        JsonElement changes = JsonDocument.Parse(_written.Changes!).RootElement;
        changes.GetProperty("errorCode").GetString().Should().Be("Test.Boom");
    }

    [Fact]
    public async Task Handle_WhenHandlerThrows_WritesFailedWithExceptionTypeAndRethrows()
    {
        Func<Task> act = async () => await CreateBehavior().Handle(
            new TestRequest("x"),
            _ => throw new InvalidOperationException("kaboom"),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();

        _written!.Action.Should().Be(AuditAction.Failed);
        JsonElement changes = JsonDocument.Parse(_written.Changes!).RootElement;
        changes.GetProperty("exceptionType").GetString().Should().Be(typeof(InvalidOperationException).FullName);
    }

    [Fact]
    public async Task Handle_MergesHandlerSuppliedMetadataIntoChanges()
    {
        await CreateBehavior().Handle(
            new TestRequest("x"),
            _ =>
            {
                // Simulates a handler attaching audit metadata during execution.
                _accumulator.Set("successfulDownload", true);
                _accumulator.Set("rowCount", 7);
                return Task.FromResult(Result.Success());
            },
            CancellationToken.None);

        JsonElement changes = JsonDocument.Parse(_written!.Changes!).RootElement;
        changes.GetProperty("successfulDownload").GetBoolean().Should().BeTrue();
        changes.GetProperty("rowCount").GetInt32().Should().Be(7);
    }

    [Fact]
    public async Task Handle_WhenSinkThrows_DoesNotBreakTheRequest()
    {
        _sink
            .Setup(s => s.WriteAsync(It.IsAny<AuditLogRecord>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("audit db down"));

        Result result = await CreateBehavior().Handle(
            new TestRequest("x"), _ => Task.FromResult(Result.Success()), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    public sealed record TestRequest(string Value) : IRequest<Result>;
}
