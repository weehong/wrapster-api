using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Wrapsfer.Application.Products;
using Wrapsfer.Application.Products.Services;
using Wrapsfer.Application.Tests.Helpers;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Repositories;
using Wrapsfer.Mailing.Abstractions;

namespace Wrapsfer.Application.Tests.Products.Services;

public class LowStockReminderProcessorTests
{
    private const string TenantId = "tenant-1";

    private readonly Mock<IMailer> _mailer = new();
    private readonly Mock<IProductRepository> _productRepository = new();
    private readonly Mock<IStockAlertLogRepository> _stockAlertLogRepository = new();
    private readonly Mock<ITenantSettingsRepository> _tenantSettingsRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private LowStockReminderProcessor CreateProcessor(int reminderIntervalHours = 24, int globalThreshold = 10)
    {
        LowStockAlertService alertService = new(
            _mailer.Object,
            _tenantSettingsRepository.Object,
            _stockAlertLogRepository.Object,
            _unitOfWork.Object,
            NullLogger<LowStockAlertService>.Instance);

        IOptions<ProductSettings> options = Options.Create(new ProductSettings
        {
            GlobalLowStockThreshold = globalThreshold,
            LowStockReminderIntervalHours = reminderIntervalHours
        });

        return new LowStockReminderProcessor(
            _productRepository.Object,
            _tenantSettingsRepository.Object,
            _stockAlertLogRepository.Object,
            alertService,
            options,
            NullLogger<LowStockReminderProcessor>.Instance);
    }

    private void SetupTenantsAndDefaults(string tenantId, int? defaultThreshold = null)
    {
        _productRepository.Setup(r => r.GetDistinctTenantIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { tenantId });
        _tenantSettingsRepository.Setup(r => r.GetDefaultThresholdsByTenantIdsAsync(
                It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, int?> { [tenantId] = defaultThreshold });
    }

    private void SetupCandidates(string tenantId, params Product[] products) =>
        _productRepository.Setup(r => r.GetLowStockCandidatesAsync(tenantId, It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(products.ToList());

    private void SetupLastSentAlerts(string tenantId, StockAlertType alertType,
        Dictionary<Guid, StockAlertLog> map) =>
        _stockAlertLogRepository.Setup(r => r.GetLastSentAlertsByProductIdsAsync(
                tenantId, It.IsAny<IEnumerable<Guid>>(), alertType, It.IsAny<CancellationToken>()))
            .ReturnsAsync(map);

    private void SetupRecipients(string tenantId, params string[] emails)
    {
        Domain.Entities.TenantSettings settings = Domain.Entities.TenantSettings.Create(tenantId).Value;
        foreach (string email in emails)
        {
            settings.AddRecipient(email);
        }

        _tenantSettingsRepository.Setup(r => r.GetByTenantIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);
        _mailer.Setup(m => m.SendAsync(It.IsAny<MailMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MailRequestId.New());
    }

    private static StockAlertLog CreateLogAt(string tenantId, Guid productId, StockAlertType type, DateTime when) =>
        StockAlertLog.Create(tenantId, productId, "Widget", "BC-001", type, 3, 10,
            StockAlertDeliveryStatus.Sent, "a@example.com", null, when);

    [Fact]
    public async Task Run_NoProductsBelowThreshold_DoesNothing()
    {
        SetupTenantsAndDefaults(TenantId);
        SetupCandidates(TenantId);

        LowStockReminderRunSummary summary = await CreateProcessor().RunAsync(CancellationToken.None);

        summary.SentCount.Should().Be(0);
        summary.SkippedCount.Should().Be(0);
        _mailer.Verify(m => m.SendAsync(It.IsAny<MailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Run_ProductWithNoPriorLowStockAlert_DoesNotSendReminder()
    {
        Product product = ProductTestFactory.CreateSingle(tenantId: TenantId, stockQuantity: 2, lowStockThreshold: 10);
        SetupTenantsAndDefaults(TenantId);
        SetupCandidates(TenantId, product);
        SetupLastSentAlerts(TenantId, StockAlertType.LowStock, new Dictionary<Guid, StockAlertLog>());
        SetupLastSentAlerts(TenantId, StockAlertType.Recovered, new Dictionary<Guid, StockAlertLog>());

        LowStockReminderRunSummary summary = await CreateProcessor().RunAsync(CancellationToken.None);

        summary.SentCount.Should().Be(0);
        _mailer.Verify(m => m.SendAsync(It.IsAny<MailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Run_ProductWithRecentLowStockAlert_SkipsReminder()
    {
        Product product = ProductTestFactory.CreateSingle(tenantId: TenantId, stockQuantity: 2, lowStockThreshold: 10);
        StockAlertLog recent = CreateLogAt(TenantId, product.Id, StockAlertType.LowStock,
            DateTime.UtcNow.AddHours(-5));
        SetupTenantsAndDefaults(TenantId);
        SetupCandidates(TenantId, product);
        SetupLastSentAlerts(TenantId, StockAlertType.LowStock,
            new Dictionary<Guid, StockAlertLog> { [product.Id] = recent });
        SetupLastSentAlerts(TenantId, StockAlertType.Recovered, new Dictionary<Guid, StockAlertLog>());

        LowStockReminderRunSummary summary = await CreateProcessor().RunAsync(CancellationToken.None);

        summary.SentCount.Should().Be(0);
        summary.SkippedCount.Should().Be(1);
        _mailer.Verify(m => m.SendAsync(It.IsAny<MailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Run_ProductWithStaleLowStockAlertAndNoRecovery_SendsReminder()
    {
        Product product = ProductTestFactory.CreateSingle(tenantId: TenantId, stockQuantity: 2, lowStockThreshold: 10);
        StockAlertLog old = CreateLogAt(TenantId, product.Id, StockAlertType.LowStock,
            DateTime.UtcNow.AddHours(-25));
        SetupTenantsAndDefaults(TenantId);
        SetupCandidates(TenantId, product);
        SetupLastSentAlerts(TenantId, StockAlertType.LowStock,
            new Dictionary<Guid, StockAlertLog> { [product.Id] = old });
        SetupLastSentAlerts(TenantId, StockAlertType.Recovered, new Dictionary<Guid, StockAlertLog>());
        SetupRecipients(TenantId, "ops@example.com");

        LowStockReminderRunSummary summary = await CreateProcessor().RunAsync(CancellationToken.None);

        summary.SentCount.Should().Be(1);
        _mailer.Verify(m => m.SendAsync(
                It.Is<MailMessage>(msg => msg.TemplateName == "low-stock-alert"),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _stockAlertLogRepository.Verify(r => r.Add(It.Is<StockAlertLog>(l =>
            l.DeliveryStatus == StockAlertDeliveryStatus.Sent && l.AlertType == StockAlertType.LowStock)), Times.Once);
    }

    [Fact]
    public async Task Run_ProductWithRecoveryNewerThanLowStock_DoesNotSendReminder()
    {
        Product product = ProductTestFactory.CreateSingle(tenantId: TenantId, stockQuantity: 2, lowStockThreshold: 10);
        StockAlertLog oldLow = CreateLogAt(TenantId, product.Id, StockAlertType.LowStock,
            DateTime.UtcNow.AddHours(-50));
        StockAlertLog recovery = CreateLogAt(TenantId, product.Id, StockAlertType.Recovered,
            DateTime.UtcNow.AddHours(-30));
        SetupTenantsAndDefaults(TenantId);
        SetupCandidates(TenantId, product);
        SetupLastSentAlerts(TenantId, StockAlertType.LowStock,
            new Dictionary<Guid, StockAlertLog> { [product.Id] = oldLow });
        SetupLastSentAlerts(TenantId, StockAlertType.Recovered,
            new Dictionary<Guid, StockAlertLog> { [product.Id] = recovery });

        LowStockReminderRunSummary summary = await CreateProcessor().RunAsync(CancellationToken.None);

        summary.SentCount.Should().Be(0);
        _mailer.Verify(m => m.SendAsync(It.IsAny<MailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Run_ProductWithNewLowStockAlertAfterRecovery_SendsReminder()
    {
        Product product = ProductTestFactory.CreateSingle(tenantId: TenantId, stockQuantity: 2, lowStockThreshold: 10);
        StockAlertLog recovery = CreateLogAt(TenantId, product.Id, StockAlertType.Recovered,
            DateTime.UtcNow.AddHours(-50));
        StockAlertLog newLow = CreateLogAt(TenantId, product.Id, StockAlertType.LowStock,
            DateTime.UtcNow.AddHours(-30));
        SetupTenantsAndDefaults(TenantId);
        SetupCandidates(TenantId, product);
        SetupLastSentAlerts(TenantId, StockAlertType.LowStock,
            new Dictionary<Guid, StockAlertLog> { [product.Id] = newLow });
        SetupLastSentAlerts(TenantId, StockAlertType.Recovered,
            new Dictionary<Guid, StockAlertLog> { [product.Id] = recovery });
        SetupRecipients(TenantId, "ops@example.com");

        LowStockReminderRunSummary summary = await CreateProcessor().RunAsync(CancellationToken.None);

        summary.SentCount.Should().Be(1);
        _mailer.Verify(m => m.SendAsync(It.IsAny<MailMessage>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Run_UsesTenantDefaultThresholdWhenProductHasNoOwn()
    {
        Product product = ProductTestFactory.CreateSingle(tenantId: TenantId, stockQuantity: 3, lowStockThreshold: null);
        StockAlertLog old = CreateLogAt(TenantId, product.Id, StockAlertType.LowStock,
            DateTime.UtcNow.AddHours(-25));
        SetupTenantsAndDefaults(TenantId, defaultThreshold: 5);
        SetupCandidates(TenantId, product);
        SetupLastSentAlerts(TenantId, StockAlertType.LowStock,
            new Dictionary<Guid, StockAlertLog> { [product.Id] = old });
        SetupLastSentAlerts(TenantId, StockAlertType.Recovered, new Dictionary<Guid, StockAlertLog>());
        SetupRecipients(TenantId, "ops@example.com");

        LowStockReminderRunSummary summary = await CreateProcessor().RunAsync(CancellationToken.None);

        summary.SentCount.Should().Be(1);
        _productRepository.Verify(r => r.GetLowStockCandidatesAsync(TenantId, 5, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Run_FallsBackToGlobalThresholdWhenNoTenantSetting()
    {
        Product product = ProductTestFactory.CreateSingle(tenantId: TenantId, stockQuantity: 3, lowStockThreshold: null);
        StockAlertLog old = CreateLogAt(TenantId, product.Id, StockAlertType.LowStock,
            DateTime.UtcNow.AddHours(-25));
        SetupTenantsAndDefaults(TenantId, defaultThreshold: null);
        SetupCandidates(TenantId, product);
        SetupLastSentAlerts(TenantId, StockAlertType.LowStock,
            new Dictionary<Guid, StockAlertLog> { [product.Id] = old });
        SetupLastSentAlerts(TenantId, StockAlertType.Recovered, new Dictionary<Guid, StockAlertLog>());
        SetupRecipients(TenantId, "ops@example.com");

        LowStockReminderRunSummary summary = await CreateProcessor(globalThreshold: 7).RunAsync(CancellationToken.None);

        summary.SentCount.Should().Be(1);
        _productRepository.Verify(r => r.GetLowStockCandidatesAsync(TenantId, 7, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
