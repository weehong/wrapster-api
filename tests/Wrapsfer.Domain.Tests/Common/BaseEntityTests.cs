using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Events;
using Wrapsfer.Domain.Tests.Helpers;

namespace Wrapsfer.Domain.Tests.Common;

public class BaseEntityTests
{
    [Fact]
    public void Id_IsGeneratedOnCreation()
    {
        Product product = ProductFactory.CreateSingle();

        product.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void AddDomainEvent_AddsEventToCollection()
    {
        Product product = ProductFactory.CreateSingle();
        LowStockDetectedEvent domainEvent = new(product.Id, "Test", "BC-1", 1, 10, "tenant", DateTime.UtcNow);

        product.AddDomainEvent(domainEvent);

        product.DomainEvents.Should().ContainSingle().Which.Should().Be(domainEvent);
    }

    [Fact]
    public void RemoveDomainEvent_RemovesEventFromCollection()
    {
        Product product = ProductFactory.CreateSingle();
        LowStockDetectedEvent domainEvent = new(product.Id, "Test", "BC-1", 1, 10, "tenant", DateTime.UtcNow);
        product.AddDomainEvent(domainEvent);

        product.RemoveDomainEvent(domainEvent);

        product.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ClearDomainEvents_ClearsAllEvents()
    {
        Product product = ProductFactory.CreateSingle();
        product.AddDomainEvent(new LowStockDetectedEvent(product.Id, "A", "BC-1", 1, 10, "t", DateTime.UtcNow));
        product.AddDomainEvent(new LowStockDetectedEvent(product.Id, "B", "BC-2", 2, 10, "t", DateTime.UtcNow));

        product.ClearDomainEvents();

        product.DomainEvents.Should().BeEmpty();
    }
}
