using Microsoft.Extensions.Logging.Abstractions;
using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Products.EventHandlers;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Events;
using Wrapsfer.Domain.Repositories;
using Wrapsfer.Mailing.Abstractions;
using TenantSettingsEntity = Wrapsfer.Domain.Entities.TenantSettings;

namespace Wrapsfer.Application.Tests.Products.EventHandlers;

public class StockRecoveredEventHandlerTests
{
    private readonly Mock<IMailer> _mailer = new();
    private readonly Mock<IStockAlertLogRepository> _stockAlertLogRepository = new();
    private readonly Mock<ITenantSettingsRepository> _tenantSettingsRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private StockRecoveredEventHandler CreateHandler() =>
        new(_mailer.Object,
            _tenantSettingsRepository.Object,
            _stockAlertLogRepository.Object,
            _unitOfWork.Object,
            NullLogger<StockRecoveredEventHandler>.Instance);

    private static DomainEventNotification<StockRecoveredEvent> CreateNotification(string tenantId = "tenant-1") =>
        new(new StockRecoveredEvent(Guid.NewGuid(), "Widget", "BC-001", 12, 10, tenantId, DateTime.UtcNow));

    private static TenantSettingsEntity CreateSettingsWithRecipients(string tenantId, params string[] emails)
    {
        TenantSettingsEntity settings = TenantSettingsEntity.Create(tenantId).Value;
        foreach (string email in emails)
        {
            settings.AddRecipient(email);
        }

        return settings;
    }

    [Fact]
    public async Task Handle_WithNoRecipients_SuppressesWithoutSendingMail()
    {
        _tenantSettingsRepository.Setup(r => r.GetByTenantIdAsync("tenant-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantSettingsEntity?)null);

        await CreateHandler().Handle(CreateNotification(), CancellationToken.None);

        _mailer.Verify(m => m.SendAsync(It.IsAny<MailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
        _stockAlertLogRepository.Verify(r => r.Add(It.Is<StockAlertLog>(l =>
            l.DeliveryStatus == StockAlertDeliveryStatus.Suppressed && l.FailureReason == "NoRecipients")), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithRecipients_SendsMailAndLogsSent()
    {
        _tenantSettingsRepository.Setup(r => r.GetByTenantIdAsync("tenant-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSettingsWithRecipients("tenant-1", "a@example.com"));
        _mailer.Setup(m => m.SendAsync(It.IsAny<MailMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MailRequestId.New());

        await CreateHandler().Handle(CreateNotification(), CancellationToken.None);

        _mailer.Verify(m => m.SendAsync(It.IsAny<MailMessage>(), It.IsAny<CancellationToken>()), Times.Once);
        _stockAlertLogRepository.Verify(r => r.Add(It.Is<StockAlertLog>(l =>
            l.DeliveryStatus == StockAlertDeliveryStatus.Sent)), Times.Once);
    }
}
