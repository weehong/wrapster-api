using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Common;
using Wrapsfer.Application.Tests.Helpers;
using Wrapsfer.Application.Waybills.Queries.ListWaybills;
using Wrapsfer.Application.Waybills.Responses;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Tests.Waybills.Queries;

public class ListWaybillsQueryHandlerTests
{
    private const string TenantId = "test-tenant";
    private readonly ListWaybillsQueryHandler _handler;

    private readonly Mock<IWaybillRepository> _waybillRepository = new();
    private readonly Mock<IProductRepository> _productRepository = new();
    private readonly Mock<IPartnerTenantRepository> _partnerTenantRepository = new();
    private readonly Mock<ITenantContext> _tenantContext = new();

    public ListWaybillsQueryHandlerTests()
    {
        _tenantContext.Setup(x => x.TenantId).Returns(TenantId);
        _handler = new ListWaybillsQueryHandler(
            _waybillRepository.Object,
            _productRepository.Object,
            _partnerTenantRepository.Object,
            _tenantContext.Object);
    }

    private static Waybill CreateWaybill(string tenantId, string number,
        Guid? productId = null, string? productBarcode = null)
    {
        Waybill waybill = Waybill.Create(tenantId, new DateOnly(2026, 5, 1), number).Value;

        if (productId.HasValue)
        {
            waybill.AddOrIncrementItem(productId.Value, productBarcode ?? "BC-1", 1);
        }

        return waybill;
    }

    [Fact]
    public async Task Handle_WhenNoWaybills_ReturnsEmptyPagedResult()
    {
        _waybillRepository.Setup(r => r.ListAsync(
                TenantId, null, null, null, null, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Array.Empty<Waybill>() as IReadOnlyList<Waybill>, 0));

        Result<PagedResult<WaybillResponse>> result = await _handler.Handle(
            new ListWaybillsQuery(null, null, null, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenTenantScoped_ListsWaybillsForCurrentTenantOnly()
    {
        Waybill waybill = CreateWaybill(TenantId, "WB-1");

        _waybillRepository.Setup(r => r.ListAsync(
                TenantId, null, null, null, null, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Waybill> { waybill } as IReadOnlyList<Waybill>, 1));

        Result<PagedResult<WaybillResponse>> result = await _handler.Handle(
            new ListWaybillsQuery(null, null, null, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle();
        result.Value.Items[0].TenantId.Should().Be(TenantId);

        _waybillRepository.Verify(r => r.ListByTenantIdsAsync(
            It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(),
            It.IsAny<Domain.Enums.WaybillStatus?>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenTenantScoped_ResolvesProductNamesWithinTenant()
    {
        Product product = ProductTestFactory.CreateSingle(tenantId: TenantId, barcode: "BC-1", name: "Widget");
        Waybill waybill = CreateWaybill(TenantId, "WB-1", product.Id, "BC-1");

        _waybillRepository.Setup(r => r.ListAsync(
                TenantId, null, null, null, null, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Waybill> { waybill } as IReadOnlyList<Waybill>, 1));
        _productRepository.Setup(r => r.GetByIdsAsync(
                It.Is<IEnumerable<Guid>>(ids => ids.Contains(product.Id)), TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { product });

        Result<PagedResult<WaybillResponse>> result = await _handler.Handle(
            new ListWaybillsQuery(null, null, null, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items[0].Items[0].ProductName.Should().Be("Widget");
    }

    [Fact]
    public async Task Handle_WhenIncludingAllPartnerTenants_ListsWaybillsAcrossPartnerTenants()
    {
        PartnerTenant partnerA = PartnerTenant.Create("partner-a", "Partner A", "owner").Value;
        PartnerTenant partnerB = PartnerTenant.Create("partner-b", "Partner B", "owner").Value;
        Waybill waybillA = CreateWaybill("partner-a", "WB-A");
        Waybill waybillB = CreateWaybill("partner-b", "WB-B");

        _partnerTenantRepository.Setup(r => r.ListAsync(It.IsAny<bool?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PartnerTenant> { partnerA, partnerB });
        _waybillRepository.Setup(r => r.ListByTenantIdsAsync(
                It.Is<IReadOnlyCollection<string>>(ids => ids.Contains("partner-a") && ids.Contains("partner-b")),
                null, null, null, null, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Waybill> { waybillA, waybillB } as IReadOnlyList<Waybill>, 2));

        Result<PagedResult<WaybillResponse>> result = await _handler.Handle(
            new ListWaybillsQuery(null, null, null, null, IncludeAllPartnerTenants: true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(2);
        result.Value.Items.Select(w => w.TenantId).Should().BeEquivalentTo("partner-a", "partner-b");

        _waybillRepository.Verify(r => r.ListAsync(
            It.IsAny<string>(), It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(), It.IsAny<Domain.Enums.WaybillStatus?>(),
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenIncludingAllPartnerTenants_ResolvesProductNamesAcrossTenants()
    {
        PartnerTenant partnerA = PartnerTenant.Create("partner-a", "Partner A", "owner").Value;
        PartnerTenant partnerB = PartnerTenant.Create("partner-b", "Partner B", "owner").Value;
        Product productA = ProductTestFactory.CreateSingle(tenantId: "partner-a", barcode: "BC-A", name: "Alpha");
        Product productB = ProductTestFactory.CreateSingle(tenantId: "partner-b", barcode: "BC-B", name: "Bravo");
        Waybill waybillA = CreateWaybill("partner-a", "WB-A", productA.Id, "BC-A");
        Waybill waybillB = CreateWaybill("partner-b", "WB-B", productB.Id, "BC-B");

        _partnerTenantRepository.Setup(r => r.ListAsync(It.IsAny<bool?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PartnerTenant> { partnerA, partnerB });
        _waybillRepository.Setup(r => r.ListByTenantIdsAsync(
                It.IsAny<IReadOnlyCollection<string>>(),
                null, null, null, null, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Waybill> { waybillA, waybillB } as IReadOnlyList<Waybill>, 2));
        _productRepository.Setup(r => r.GetByIdsByTenantIdsAsync(
                It.Is<IEnumerable<Guid>>(ids => ids.Contains(productA.Id) && ids.Contains(productB.Id)),
                It.Is<IReadOnlyCollection<string>>(ids => ids.Contains("partner-a") && ids.Contains("partner-b")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { productA, productB });

        Result<PagedResult<WaybillResponse>> result = await _handler.Handle(
            new ListWaybillsQuery(null, null, null, null, IncludeAllPartnerTenants: true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.First(w => w.TenantId == "partner-a").Items[0].ProductName.Should().Be("Alpha");
        result.Value.Items.First(w => w.TenantId == "partner-b").Items[0].ProductName.Should().Be("Bravo");

        _productRepository.Verify(r => r.GetByIdsAsync(
            It.IsAny<IEnumerable<Guid>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
