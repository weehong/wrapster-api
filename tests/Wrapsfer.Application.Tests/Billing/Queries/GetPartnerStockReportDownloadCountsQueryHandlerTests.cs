using Wrapsfer.Application.Billing.Queries.GetPartnerStockReportDownloadCounts;
using Wrapsfer.Application.Billing.Responses;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Tests.Billing.Queries;

public class GetPartnerStockReportDownloadCountsQueryHandlerTests
{
    private readonly Mock<IAuditLogRepository> _auditLogRepository = new();
    private readonly GetPartnerStockReportDownloadCountsQueryHandler _handler;

    private string? _capturedTenantId;

    public GetPartnerStockReportDownloadCountsQueryHandlerTests()
    {
        _auditLogRepository
            .Setup(r => r.GetSuccessfulStockReportDownloadsAsync(
                It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback<string, DateTime, DateTime, CancellationToken>((tenantId, _, _, _) => _capturedTenantId = tenantId)
            .ReturnsAsync(new List<StockReportDownloadAudit>
            {
                new(new DateTime(2026, 6, 2, 9, 0, 0, DateTimeKind.Utc), "user-9", "carol")
            });

        _handler = new GetPartnerStockReportDownloadCountsQueryHandler(_auditLogRepository.Object);
    }

    [Fact]
    public async Task Handle_QueriesTheRequestedTenant()
    {
        Result<IReadOnlyList<StockReportMonthlyDownloadCount>> result = await _handler.Handle(
            new GetPartnerStockReportDownloadCountsQuery("partner-b", "2026-06", "2026-06"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _capturedTenantId.Should().Be("partner-b");
        result.Value.Single().TenantId.Should().Be("partner-b");
        result.Value.Single().Users.Single().Username.Should().Be("carol");
    }

    [Fact]
    public async Task Handle_WhenTenantIdMissing_ReturnsInvalidTenantId()
    {
        Result<IReadOnlyList<StockReportMonthlyDownloadCount>> result = await _handler.Handle(
            new GetPartnerStockReportDownloadCountsQuery("  ", "2026-06", "2026-06"),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BillingErrors.InvalidTenantId);
        _auditLogRepository.Verify(
            r => r.GetSuccessfulStockReportDownloadsAsync(
                It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
