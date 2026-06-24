using Microsoft.Extensions.Logging.Abstractions;
using Wrapsfer.Application.Products.Services;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Repositories;
using Wrapsfer.Mailing.Abstractions;

namespace Wrapsfer.Application.Tests.Products.Services;

public class LowStockAlertServiceTests
{
    private readonly Mock<IMailer> _mailer = new();
    private readonly Mock<IStockAlertLogRepository> _stockAlertLogRepository = new();
    private readonly Mock<ITenantSettingsRepository> _tenantSettingsRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private LowStockAlertService CreateService() =>
        new(_mailer.Object,
            _tenantSettingsRepository.Object,
            _stockAlertLogRepository.Object,
            _unitOfWork.Object,
            NullLogger<LowStockAlertService>.Instance);

    private static LowStockAlertContext CreateContext(string tenantId = "tenant-1", Guid? productId = null) =>
        new(tenantId, productId ?? Guid.NewGuid(), "Widget", "BC-001", 3, 10);

    private static Domain.Entities.TenantSettings CreateSettingsWithRecipients(string tenantId, params string[] emails)
    {
        Domain.Entities.TenantSettings settings = Domain.Entities.TenantSettings.Create(tenantId).Value;
        foreach (string email in emails)
        {
            settings.AddRecipient(email);
        }

        return settings;
    }

    [Fact]
    public async Task SendAsync_WithNoRecipients_SuppressesAndReturnsNoRecipients()
    {
        _tenantSettingsRepository.Setup(r => r.GetByTenantIdAsync("tenant-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Domain.Entities.TenantSettings?)null);

        LowStockAlertOutcome outcome = await CreateService()
            .SendAsync(CreateContext(), TimeSpan.FromHours(24), 3, CancellationToken.None);

        outcome.Should().Be(LowStockAlertOutcome.NoRecipients);
        _mailer.Verify(m => m.SendAsync(It.IsAny<MailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
        _stockAlertLogRepository.Verify(r => r.Add(It.Is<StockAlertLog>(l =>
            l.DeliveryStatus == StockAlertDeliveryStatus.Suppressed && l.FailureReason == "NoRecipients")), Times.Once);
    }

    [Fact]
    public async Task SendAsync_WithRecipientsAndNoRecentAlert_SendsMailAndLogsSent()
    {
        _tenantSettingsRepository.Setup(r => r.GetByTenantIdAsync("tenant-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSettingsWithRecipients("tenant-1", "a@example.com"));
        _mailer.Setup(m => m.SendAsync(It.IsAny<MailMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MailRequestId.New());

        LowStockAlertOutcome outcome = await CreateService()
            .SendAsync(CreateContext(), TimeSpan.FromHours(24), 3, CancellationToken.None);

        outcome.Should().Be(LowStockAlertOutcome.Sent);
        _mailer.Verify(m => m.SendAsync(It.IsAny<MailMessage>(), It.IsAny<CancellationToken>()), Times.Once);
        _stockAlertLogRepository.Verify(r => r.Add(It.Is<StockAlertLog>(l =>
            l.DeliveryStatus == StockAlertDeliveryStatus.Sent)), Times.Once);
    }

    [Fact]
    public async Task SendAsync_WithRecentAlertWithinDedupeWindow_SuppressesAndSkipsMail()
    {
        Guid productId = Guid.NewGuid();
        StockAlertLog lastSent = StockAlertLog.Create(
            "tenant-1", productId, "Widget", "BC-001",
            StockAlertType.LowStock, 3, 10,
            StockAlertDeliveryStatus.Sent, "a@example.com", null,
            DateTime.UtcNow.AddHours(-1));
        _stockAlertLogRepository.Setup(r => r.GetLastAlertAsync("tenant-1", productId,
                StockAlertType.LowStock, StockAlertDeliveryStatus.Sent, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lastSent);

        LowStockAlertOutcome outcome = await CreateService()
            .SendAsync(CreateContext(productId: productId), TimeSpan.FromHours(24), 3, CancellationToken.None);

        outcome.Should().Be(LowStockAlertOutcome.Suppressed);
        _mailer.Verify(m => m.SendAsync(It.IsAny<MailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
        _stockAlertLogRepository.Verify(r => r.Add(It.Is<StockAlertLog>(l =>
            l.DeliveryStatus == StockAlertDeliveryStatus.Suppressed)), Times.Once);
    }

    [Fact]
    public async Task SendAsync_WithLastSentBeyondDedupeWindow_SendsAgain()
    {
        Guid productId = Guid.NewGuid();
        StockAlertLog oldSent = StockAlertLog.Create(
            "tenant-1", productId, "Widget", "BC-001",
            StockAlertType.LowStock, 3, 10,
            StockAlertDeliveryStatus.Sent, "a@example.com", null,
            DateTime.UtcNow.AddHours(-25));
        _stockAlertLogRepository.Setup(r => r.GetLastAlertAsync("tenant-1", productId,
                StockAlertType.LowStock, StockAlertDeliveryStatus.Sent, It.IsAny<CancellationToken>()))
            .ReturnsAsync(oldSent);
        _tenantSettingsRepository.Setup(r => r.GetByTenantIdAsync("tenant-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSettingsWithRecipients("tenant-1", "a@example.com"));
        _mailer.Setup(m => m.SendAsync(It.IsAny<MailMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MailRequestId.New());

        LowStockAlertOutcome outcome = await CreateService()
            .SendAsync(CreateContext(productId: productId), TimeSpan.FromHours(24), 3, CancellationToken.None);

        outcome.Should().Be(LowStockAlertOutcome.Sent);
        _mailer.Verify(m => m.SendAsync(It.IsAny<MailMessage>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendAsync_WhenMaxAlertsReachedForEpisode_SuppressesAndSkipsMail()
    {
        Guid productId = Guid.NewGuid();
        _stockAlertLogRepository.Setup(r => r.CountSentLowStockAlertsInCurrentEpisodeAsync(
                "tenant-1", productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        LowStockAlertOutcome outcome = await CreateService()
            .SendAsync(CreateContext(productId: productId), TimeSpan.FromHours(168), 3, CancellationToken.None);

        outcome.Should().Be(LowStockAlertOutcome.MaxReached);
        _mailer.Verify(m => m.SendAsync(It.IsAny<MailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
        _stockAlertLogRepository.Verify(r => r.Add(It.Is<StockAlertLog>(l =>
            l.DeliveryStatus == StockAlertDeliveryStatus.Suppressed && l.FailureReason == "MaxAlertsReached")), Times.Once);
    }
}
