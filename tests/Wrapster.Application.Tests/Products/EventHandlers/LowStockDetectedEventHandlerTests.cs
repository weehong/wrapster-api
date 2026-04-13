using Microsoft.Extensions.Logging.Abstractions;
using Wrapster.Application.Abstractions;
using Wrapster.Application.Abstractions.Email;
using Wrapster.Application.Products.EventHandlers;
using Wrapster.Domain.Abstractions;
using Wrapster.Domain.Common;
using Wrapster.Domain.Entities;
using Wrapster.Domain.Enums;
using Wrapster.Domain.Events;
using Wrapster.Domain.Repositories;

namespace Wrapster.Application.Tests.Products.EventHandlers;

public class LowStockDetectedEventHandlerTests
{
    private readonly Mock<IEmailService> _emailService = new();
    private readonly Mock<ITenantSettingsRepository> _tenantSettingsRepository = new();
    private readonly Mock<IStockAlertLogRepository> _stockAlertLogRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private LowStockDetectedEventHandler CreateHandler() =>
        new(_emailService.Object,
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

    private static Wrapster.Domain.Entities.TenantSettings CreateSettingsWithRecipients(string tenantId, params string[] emails)
    {
        Wrapster.Domain.Entities.TenantSettings settings = Wrapster.Domain.Entities.TenantSettings.Create(tenantId).Value;
        foreach (string email in emails)
        {
            settings.AddRecipient(email);
        }

        return settings;
    }

    [Fact]
    public async Task Handle_WhenNoRecipients_DoesNotSendEmailAndLogsFailure()
    {
        _tenantSettingsRepository.Setup(r => r.GetByTenantIdAsync("tenant-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Wrapster.Domain.Entities.TenantSettings?)null);

        await CreateHandler().Handle(CreateNotification(), CancellationToken.None);

        _emailService.Verify(
            e => e.SendAsync(It.IsAny<string>(), It.IsAny<IEmailTemplate>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _stockAlertLogRepository.Verify(r => r.Add(It.Is<StockAlertLog>(l =>
            l.DeliveryStatus == StockAlertDeliveryStatus.Failed && l.FailureReason == "NoRecipients")), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenRecipientsConfigured_SendsEmailAndLogsSent()
    {
        Wrapster.Domain.Entities.TenantSettings settings = CreateSettingsWithRecipients("tenant-1", "a@example.com", "b@example.com");
        _tenantSettingsRepository.Setup(r => r.GetByTenantIdAsync("tenant-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);
        _emailService.Setup(e =>
                e.SendAsync(It.IsAny<string>(), It.IsAny<IEmailTemplate>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        await CreateHandler().Handle(CreateNotification(), CancellationToken.None);

        _emailService.Verify(
            e => e.SendAsync(It.IsAny<string>(), It.IsAny<IEmailTemplate>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
        _stockAlertLogRepository.Verify(r => r.Add(It.Is<StockAlertLog>(l =>
            l.DeliveryStatus == StockAlertDeliveryStatus.Sent && l.AlertType == StockAlertType.LowStock)), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenRecentSentAlertExists_SuppressesAndLogs()
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

        _emailService.Verify(
            e => e.SendAsync(It.IsAny<string>(), It.IsAny<IEmailTemplate>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _stockAlertLogRepository.Verify(r => r.Add(It.Is<StockAlertLog>(l =>
            l.DeliveryStatus == StockAlertDeliveryStatus.Suppressed)), Times.Once);
    }
}
