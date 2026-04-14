using Wrapster.Domain.Common;
using Wrapster.Domain.Entities;
using Wrapster.Domain.Errors;
using Wrapster.Domain.Tests.Helpers;

namespace Wrapster.Domain.Tests.Entities;

public class ProductReservationTests
{
    [Fact]
    public void Reserve_WithAvailableStock_IncrementsReservedQuantity()
    {
        Product product = ProductFactory.CreateSingle(stockQuantity: 10);

        Result result = product.Reserve(3);

        result.IsSuccess.Should().BeTrue();
        product.ReservedQuantity.Should().Be(3);
        product.AvailableQuantity.Should().Be(7);
        product.StockQuantity.Should().Be(10);
    }

    [Fact]
    public void Reserve_BeyondAvailable_ReturnsInsufficientStock()
    {
        Product product = ProductFactory.CreateSingle(stockQuantity: 5);
        product.Reserve(3);

        Result result = product.Reserve(3);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ProductErrors.InsufficientStock.Code);
        product.ReservedQuantity.Should().Be(3);
    }

    [Fact]
    public void Reserve_WithZeroQuantity_ReturnsInvalidReservationQuantity()
    {
        Product product = ProductFactory.CreateSingle(stockQuantity: 10);

        Result result = product.Reserve(0);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ProductErrors.InvalidReservationQuantity.Code);
    }

    [Fact]
    public void Reserve_OnBundleProduct_ReturnsCannotReserveBundleStock()
    {
        Product bundle = ProductFactory.CreateBundle();

        Result result = bundle.Reserve(1);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ProductErrors.CannotReserveBundleStock.Code);
    }

    [Fact]
    public void Release_WithReservedStock_DecrementsReservedQuantity()
    {
        Product product = ProductFactory.CreateSingle(stockQuantity: 10);
        product.Reserve(4);

        Result result = product.Release(3);

        result.IsSuccess.Should().BeTrue();
        product.ReservedQuantity.Should().Be(1);
        product.AvailableQuantity.Should().Be(9);
        product.StockQuantity.Should().Be(10);
    }

    [Fact]
    public void Release_OverReserved_ReturnsReservationMismatch()
    {
        Product product = ProductFactory.CreateSingle(stockQuantity: 10);
        product.Reserve(2);

        Result result = product.Release(5);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ProductErrors.ReservationMismatch.Code);
        product.ReservedQuantity.Should().Be(2);
    }

    [Fact]
    public void Consume_ReducesBothOnHandAndReserved()
    {
        Product product = ProductFactory.CreateSingle(stockQuantity: 10);
        product.Reserve(4);

        Result result = product.Consume(3);

        result.IsSuccess.Should().BeTrue();
        product.StockQuantity.Should().Be(7);
        product.ReservedQuantity.Should().Be(1);
        product.AvailableQuantity.Should().Be(6);
    }

    [Fact]
    public void Consume_WhenReservedLessThanQty_ReturnsReservationMismatch()
    {
        Product product = ProductFactory.CreateSingle(stockQuantity: 10);
        product.Reserve(2);

        Result result = product.Consume(3);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ProductErrors.ReservationMismatch.Code);
        product.StockQuantity.Should().Be(10);
        product.ReservedQuantity.Should().Be(2);
    }

    [Fact]
    public void Consume_OnBundleProduct_ReturnsCannotDeductBundleStock()
    {
        Product bundle = ProductFactory.CreateBundle();

        Result result = bundle.Consume(1);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ProductErrors.CannotDeductBundleStock.Code);
    }
}
