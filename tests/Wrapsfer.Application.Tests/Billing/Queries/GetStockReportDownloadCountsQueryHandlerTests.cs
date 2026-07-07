using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Billing.Queries.GetStockReportDownloadCounts;
using Wrapsfer.Application.Billing.Responses;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Tests.Billing.Queries;

public class GetStockReportDownloadCountsQueryHandlerTests
{
    private readonly Mock<ITenantContext> _tenantContext = new();
    private readonly Mock<IAuditLogRepository> _auditLogRepository = new();
    private readonly GetStockReportDownloadCountsQueryHandler _handler;

    private string? _capturedTenantId;
    private DateTime _capturedFrom;
    private DateTime _capturedTo;

    public GetStockReportDownloadCountsQueryHandlerTests()
    {
        _tenantContext.Setup(t => t.TenantId).Returns("partner-a");

        _auditLogRepository
            .Setup(r => r.GetSuccessfulStockReportDownloadsAsync(
                It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback<string, DateTime, DateTime, CancellationToken>((tenantId, from, to, _) =>
            {
                _capturedTenantId = tenantId;
                _capturedFrom = from;
                _capturedTo = to;
            })
            .ReturnsAsync(new List<StockReportDownloadAudit>
            {
                new(new DateTime(2026, 5, 4, 9, 0, 0, DateTimeKind.Utc), "user-1", "alice"),
                new(new DateTime(2026, 5, 20, 9, 0, 0, DateTimeKind.Utc), "user-1", "alice"),
                new(new DateTime(2026, 5, 21, 9, 0, 0, DateTimeKind.Utc), "user-2", "bob"),
                new(new DateTime(2026, 6, 2, 9, 0, 0, DateTimeKind.Utc), "user-2", "bob")
            });

        _handler = new GetStockReportDownloadCountsQueryHandler(
            _tenantContext.Object, _auditLogRepository.Object);
    }

    [Fact]
    public async Task Handle_ScopesToCurrentTenantAndConvertsMonthsToHalfOpenUtcRange()
    {
        await _handler.Handle(new GetStockReportDownloadCountsQuery("2026-05", "2026-06"), CancellationToken.None);

        _capturedTenantId.Should().Be("partner-a");
        _capturedFrom.Should().Be(new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc));
        // Half-open: through the end of June means strictly before 1 July.
        _capturedTo.Should().Be(new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task Handle_GroupsByMonthAndUser()
    {
        Result<IReadOnlyList<StockReportMonthlyDownloadCount>> result =
            await _handler.Handle(new GetStockReportDownloadCountsQuery("2026-05", "2026-06"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);

        StockReportMonthlyDownloadCount may = result.Value.Single(m => m.Month == "2026-05");
        may.TenantId.Should().Be("partner-a");
        may.SuccessfulDownloadCount.Should().Be(3);
        may.Users.Single(u => u.Username == "alice").SuccessfulDownloadCount.Should().Be(2);
        may.Users.Single(u => u.Username == "bob").SuccessfulDownloadCount.Should().Be(1);

        StockReportMonthlyDownloadCount june = result.Value.Single(m => m.Month == "2026-06");
        june.SuccessfulDownloadCount.Should().Be(1);
        june.Users.Should().ContainSingle(u => u.Username == "bob" && u.SuccessfulDownloadCount == 1);
    }
}
