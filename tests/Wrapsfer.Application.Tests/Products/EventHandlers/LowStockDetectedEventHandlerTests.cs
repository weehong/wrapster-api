using Microsoft.Extensions.Logging.Abstractions;
using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Products.EventHandlers;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Events;
using Wrapsfer.Domain.Repositories;
using Wrapsfer.Mailing.Abstractions;

namespace Wrapsfer.Application.Tests.Products.EventHandlers;

public class LowStockDetectedEventHandlerTests
{
    private readonly Mock<IMailer> _mailer = new();
    private readonly Mock<IStockAlertLogRepository> _stockAlertLogRepository = new();
    private readonly Mock<ITenantSettingsRepository> _tenantSettingsRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private LowStockDetectedEventHandler CreateHandler() =>
        new(_mailer.Object,
            _tenantSettingsRepository.Object,
            _stockAlertLogRepository.Object,
            _unitOfWork.Object,
            NullLogger<LowStockDetectedEventHandler>.Instance);

    private static DomainEventNotification<LowStockDetectedEvent> CreateNotification(string tenantId = "tenant-1",
        Guid? productId = null)
    {
        LowStockDetectedEvent domainEvent = new(
            productId ?? Guid.NewGuid(), "Widget", "BC-001", 3, 10, tenantId, DateTime.UtcNow);
        return new DomainEventNotification<LowStockDetectedEvent>(domainEvent);
    }

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
    public async Task Handle_WhenNoRecipients_DoesNotEnqueueMail()
    {
        _tenantSettingsRepository.Setup(r => r.GetByTenantIdAsync("tenant-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Domain.Entities.TenantSettings?)null);

        await CreateHandler().Handle(CreateNotification(), CancellationToken.None);

        _mailer.Verify(
            m => m.SendAsync(It.IsAny<MailMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _stockAlertLogRepository.Verify(r => r.Add(It.Is<StockAlertLog>(l =>
            l.DeliveryStatus == StockAlertDeliveryStatus.Failed && l.FailureReason == "NoRecipients")), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenRecipientsConfigured_EnqueuesSingleMailWithAllRecipients()
    {
        Domain.Entities.TenantSettings settings =
            CreateSettingsWithRecipients("tenant-1", "a@example.com", "b@example.com");
        _tenantSettingsRepository.Setup(r => r.GetByTenantIdAsync("tenant-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);
        _mailer.Setup(m => m.SendAsync(It.IsAny<MailMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MailRequestId.New());

        await CreateHandler().Handle(CreateNotification(), CancellationToken.None);

        _mailer.Verify(
            m => m.SendAsync(
                It.Is<MailMessage>(msg =>
                    msg.TemplateName == "low-stock-alert"
                    && msg.To.Count == 2
                    && msg.To.Contains("a@example.com")
                    && msg.To.Contains("b@example.com")),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _stockAlertLogRepository.Verify(r => r.Add(It.Is<StockAlertLog>(l =>
            l.DeliveryStatus == StockAlertDeliveryStatus.Sent && l.AlertType == StockAlertType.LowStock)), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenRecentSentAlertExists_SuppressesAndSkipsMailer()
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

        await CreateHandler().Handle(CreateNotification("tenant-1", productId), CancellationToken.None);

        _mailer.Verify(
            m => m.SendAsync(It.IsAny<MailMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _stockAlertLogRepository.Verify(r => r.Add(It.Is<StockAlertLog>(l =>
            l.DeliveryStatus == StockAlertDeliveryStatus.Suppressed)), Times.Once);
    }
}
